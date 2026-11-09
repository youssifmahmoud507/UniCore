using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Grading.Specifications;
using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Grading
{
    // Grades are entered by the instructor assigned to the section (Grades.Enter on the Section scope), while the grade submission
    // window of the semester is open and the section's grading scheme is published. Order: permission alone -> load -> scope ->
    // window -> scheme -> state -> change -> save + audit.
    public sealed class GradeEntryHandler(
        GradingAccess access, GradingEngine engine, ICurrentUser user, IEnrollmentReadService roster, IAggregateRepository<Grade> grades,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly GradingAccess _access = access;
        private readonly GradingEngine _engine = engine;
        private readonly ICurrentUser _user = user;
        private readonly IEnrollmentReadService _roster = roster;
        private readonly IAggregateRepository<Grade> _grades = grades;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(Grade g)
            => $"Status={g.Status};Percentage={(g.Percentage is { } p ? p.ToString() : "-")};Letter={g.Letter ?? "-"};Scores={string.Join('|', g.Scores.OrderBy(s => s.AssessmentComponentId).Select(s => $"{s.AssessmentComponentId:N}:{s.Score}"))}";

        public async Task<Result<GradeSummary>> EnterScoresAsync(EnterScoresCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Grades.Enter, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<GradeSummary>.Fail(permission.Errors);
            }

            if (command.StudentId == Guid.Empty || command.Scores is null || command.Scores.Count == 0)
            {
                return GradingErrors.Invalid;
            }

            var section = await _access.AuthorizeAssignedInstructorAsync(Permissions.Grades.Enter, command.CourseSectionId, openSemester: true, cancellationToken);
            if (!section.TryGetValue(out var context))
            {
                return Result<GradeSummary>.Fail(section.Errors);
            }

            var entries = await _roster.GetRosterAsync(command.CourseSectionId, cancellationToken);
            if (!entries.TryGetValue(out var list))
            {
                return Result<GradeSummary>.Fail(entries.Errors);
            }

            var entry = list.FirstOrDefault(e => e.StudentId == command.StudentId);
            if (entry is null)
            {
                return GradingErrors.StudentNotOnRoster;
            }

            var grade = await _grades.FirstOrDefaultAsync(new GradeOfEnrollmentSpecification(entry.EnrollmentId, tracking: true), cancellationToken);
            if (grade is { IsFinal: true })
            {
                return GradingErrors.Locked;
            }

            // A final grade is protected whatever the window says; then the window; then the scheme.
            if (!context.Semester.IsGradeSubmissionOpen(_clock.UtcToday))
            {
                return GradingErrors.WindowClosed;
            }

            var scheme = await _engine.LoadSchemeAsync(command.CourseSectionId, mustBePublished: true, cancellationToken);
            if (!scheme.TryGetValue(out var info))
            {
                return Result<GradeSummary>.Fail(scheme.Errors);
            }

            var isNew = grade is null;
            if (grade is null)
            {
                var created = Grade.Create(entry.EnrollmentId, entry.StudentId, command.CourseSectionId, info.AssessmentId, context.Section.AcademicSemesterId, context.Section.CourseId, context.Section.Credits, _clock.UtcNow);
                if (!created.TryGetValue(out grade))
                {
                    return Result<GradeSummary>.Fail(created.Errors);
                }
            }

            var before = isNew ? null : Describe(grade);

            foreach (var input in command.Scores)
            {
                var component = info.Components.FirstOrDefault(c => c.ComponentId == input.ComponentId);
                if (component is null)
                {
                    return GradingErrors.ComponentNotInScheme;
                }

                if (input.Score > component.MaxScore)
                {
                    return GradingErrors.ScoreInvalid;
                }

                var set = grade.SetScore(input.ComponentId, input.Score, _user.UserId, _clock.UtcNow);
                if (set.IsFailure)
                {
                    return Result<GradeSummary>.Fail(set.Errors);
                }
            }

            if (isNew)
            {
                _grades.Add(grade);
            }
            else
            {
                _grades.Update(grade);
            }

            var entryAudit = new AuditEntry(GradingAuditActions.ScoresEntered, _user.UserId, "Grade", grade.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(grade));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entryAudit, cancellationToken);

            return committed.IsFailure ? Result<GradeSummary>.Fail(committed.Errors) : GradingMapper.ToSummary(grade);
        }

        // Draft -> Submitted for the whole section: every registered student needs a grade with a score for every component.
        public async Task<Result<SectionStepSummary>> SubmitSectionAsync(Guid sectionId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Grades.Enter, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<SectionStepSummary>.Fail(permission.Errors);
            }

            var section = await _access.AuthorizeAssignedInstructorAsync(Permissions.Grades.Enter, sectionId, openSemester: true, cancellationToken);
            if (!section.TryGetValue(out var context))
            {
                return Result<SectionStepSummary>.Fail(section.Errors);
            }

            if (!context.Semester.IsGradeSubmissionOpen(_clock.UtcToday))
            {
                return GradingErrors.WindowClosed;
            }

            var scheme = await _engine.LoadSchemeAsync(sectionId, mustBePublished: true, cancellationToken);
            if (!scheme.TryGetValue(out var info))
            {
                return Result<SectionStepSummary>.Fail(scheme.Errors);
            }

            var entries = await _roster.GetRosterAsync(sectionId, cancellationToken);
            if (!entries.TryGetValue(out var list))
            {
                return Result<SectionStepSummary>.Fail(entries.Errors);
            }

            var grades = await _grades.ListAsync(new GradesOfSectionSpecification(sectionId, tracking: true), cancellationToken);
            if (list.Any(e => grades.All(g => g.EnrollmentId != e.EnrollmentId)))
            {
                return GradingErrors.SectionIncomplete;
            }

            var drafts = grades.Where(g => g.Status == GradeStatus.Draft).ToList();
            if (drafts.Count == 0)
            {
                return GradingErrors.NothingToProcess;
            }

            var componentIds = info.Components.Select(c => c.ComponentId).ToList();

            foreach (var grade in drafts)
            {
                var result = await _engine.CalculateAsync(info, grade, cancellationToken);
                if (!result.TryGetValue(out var calculated))
                {
                    return Result<SectionStepSummary>.Fail(result.Errors);
                }

                var submitted = grade.Submit(componentIds, calculated, _user.UserId, _clock.UtcNow);
                if (submitted.IsFailure)
                {
                    return Result<SectionStepSummary>.Fail(submitted.Errors);
                }

                _grades.Update(grade);
            }

            var sheetAudit = new AuditEntry(GradingAuditActions.Submitted, _user.UserId, "GradeSheet", sectionId.ToString(), request.IpAddress, request.UserAgent, $"Status=Draft;Count={drafts.Count}", $"Status=Submitted;Count={drafts.Count}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, sheetAudit, cancellationToken);

            return committed.IsFailure ? Result<SectionStepSummary>.Fail(committed.Errors) : new SectionStepSummary(sectionId, "Submit", GradeStatus.Submitted, drafts.Count);
        }
    }
}
