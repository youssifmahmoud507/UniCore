using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Grading.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Grading
{
    // The steps after the instructor submits, each for the whole section: review (Grades.Review, head), approve (Grades.Approve, dean),
    // publish and lock (Grades.Publish, registrar), return to the instructor (Grades.Review). Each needs the permission on the department
    // of the section's course. A step moves only the grades that are in the state it works on; the others stay as they are.
    // Order: permission alone -> load -> scope -> state -> change -> save + audit (one audit entry for the section).
    public sealed class GradeWorkflowHandler(
        GradingAccess access, ICurrentUser user, IAggregateRepository<Grade> grades, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly GradingAccess _access = access;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Grade> _grades = grades;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public Task<Result<SectionStepSummary>> ReviewSectionAsync(Guid sectionId, RequestInfo request, CancellationToken cancellationToken = default)
            => StepAsync(sectionId, Permissions.Grades.Review, "Review", [GradeStatus.Submitted], GradeStatus.Reviewed, GradingAuditActions.Reviewed, null,
                (g, now) => g.Review(_user.UserId ?? Guid.Empty, now), request, cancellationToken);

        public Task<Result<SectionStepSummary>> ApproveSectionAsync(Guid sectionId, RequestInfo request, CancellationToken cancellationToken = default)
            => StepAsync(sectionId, Permissions.Grades.Approve, "Approve", [GradeStatus.Reviewed], GradeStatus.Approved, GradingAuditActions.Approved, null,
                (g, now) => g.Approve(_user.UserId ?? Guid.Empty, now), request, cancellationToken);

        // Not before the grade publication date of the semester.
        public Task<Result<SectionStepSummary>> PublishSectionAsync(Guid sectionId, RequestInfo request, CancellationToken cancellationToken = default)
            => StepAsync(sectionId, Permissions.Grades.Publish, "Publish", [GradeStatus.Approved], GradeStatus.Published, GradingAuditActions.Published,
                semester => semester.IsGradePublicationReached(_clock.UtcToday) ? Result.Ok() : Result.Fail(GradingErrors.PublicationDateNotReached),
                (g, now) => g.Publish(now), request, cancellationToken);

        public Task<Result<SectionStepSummary>> LockSectionAsync(Guid sectionId, RequestInfo request, CancellationToken cancellationToken = default)
            => StepAsync(sectionId, Permissions.Grades.Publish, "Lock", [GradeStatus.Published], GradeStatus.Locked, GradingAuditActions.Locked, null,
                (g, now) => g.Lock(now), request, cancellationToken);

        public Task<Result<SectionStepSummary>> ReturnSectionAsync(Guid sectionId, string? reason, RequestInfo request, CancellationToken cancellationToken = default)
            => StepAsync(sectionId, Permissions.Grades.Review, "Return", [GradeStatus.Submitted, GradeStatus.Reviewed], GradeStatus.Draft, GradingAuditActions.Returned, null,
                (g, now) => g.ReturnToDraft(reason, now), request, cancellationToken);

        private async Task<Result<SectionStepSummary>> StepAsync(
            Guid sectionId, string permission, string step, GradeStatus[] fromStates, GradeStatus to, string auditAction,
            Func<UniCore.Domain.Modules.Calendar.AcademicSemester, Result>? semesterCheck, Func<Grade, DateTimeOffset, Result> apply,
            RequestInfo request, CancellationToken cancellationToken)
        {
            var allowed = await _access.RequireAsync(permission, cancellationToken);
            if (allowed.IsFailure)
            {
                return Result<SectionStepSummary>.Fail(allowed.Errors);
            }

            // Publishing and locking still work after the semester is closed to new grades; the others need an open semester.
            var openSemester = to is not (GradeStatus.Published or GradeStatus.Locked);
            var section = await _access.AuthorizeDepartmentAsync(permission, sectionId, openSemester, cancellationToken);
            if (!section.TryGetValue(out var context))
            {
                return Result<SectionStepSummary>.Fail(section.Errors);
            }

            if (semesterCheck is not null)
            {
                var checkedSemester = semesterCheck(context.Semester);
                if (checkedSemester.IsFailure)
                {
                    return Result<SectionStepSummary>.Fail(checkedSemester.Errors);
                }
            }

            var grades = await _grades.ListAsync(new GradesOfSectionSpecification(sectionId, tracking: true), cancellationToken);
            var targets = grades.Where(g => fromStates.Contains(g.Status)).ToList();
            if (targets.Count == 0)
            {
                return GradingErrors.NothingToProcess;
            }

            foreach (var grade in targets)
            {
                var changed = apply(grade, _clock.UtcNow);
                if (changed.IsFailure)
                {
                    return Result<SectionStepSummary>.Fail(changed.Errors);
                }

                _grades.Update(grade);
            }

            var entry = new AuditEntry(auditAction, _user.UserId, "GradeSheet", sectionId.ToString(), request.IpAddress, request.UserAgent, $"Status={string.Join('/', fromStates)};Count={targets.Count}", $"Status={to};Count={targets.Count}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<SectionStepSummary>.Fail(committed.Errors) : new SectionStepSummary(sectionId, step, to, targets.Count);
        }
    }
}
