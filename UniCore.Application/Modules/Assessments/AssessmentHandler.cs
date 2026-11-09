using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Assessments.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Assessments;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Assessments
{
    // The grading scheme of a section. Assessments.Read to read, Assessments.Manage (on the section or its department) to change.
    // Order of every change: permission alone -> load -> scope (and open semester) -> state -> change -> save + audit.
    public sealed class AssessmentHandler(
        AssessmentAccess access, ICurrentUser user, IAggregateRepository<Assessment> assessments, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly AssessmentAccess _access = access;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Assessment> _assessments = assessments;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private sealed record Loaded(Assessment Assessment, AssessmentAccess.SectionContext Section);

        private static string Describe(Assessment a)
            => $"Section={a.CourseSectionId};Status={a.Status};TotalWeight={a.TotalWeight};Components={string.Join('|', a.Components.OrderBy(c => c.Name).Select(c => $"{c.Name}:{c.Type}:{c.Weight}:{c.MaxScore}:{(c.DueDate is { } d ? d.ToString("yyyy-MM-dd") : "-")}"))}";

        private async Task<Result<AssessmentSummary>> CommitAsync(Assessment assessment, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "Assessment", assessment.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(assessment));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<AssessmentSummary>.Fail(committed.Errors) : AssessmentMapper.ToSummary(assessment);
        }

        // Manage permission -> load tracked -> section scope + open semester.
        private async Task<Result<Loaded>> LoadForChangeAsync(Guid assessmentId, CancellationToken cancellationToken)
        {
            var permission = await _access.RequireAsync(Permissions.Assessments.Manage, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<Loaded>.Fail(permission.Errors);
            }

            var assessment = await _assessments.FirstOrDefaultAsync(new AssessmentByIdSpecification(assessmentId, tracking: true), cancellationToken);
            if (assessment is null)
            {
                return AssessmentErrors.NotFound;
            }

            var section = await _access.AuthorizeSectionAsync(Permissions.Assessments.Manage, assessment.CourseSectionId, openSemester: true, cancellationToken);

            return section.TryGetValue(out var context) ? new Loaded(assessment, context) : Result<Loaded>.Fail(section.Errors);
        }

        // ---------- change ----------

        public async Task<Result<AssessmentSummary>> CreateAsync(CreateAssessmentCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Assessments.Manage, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<AssessmentSummary>.Fail(permission.Errors);
            }

            var section = await _access.AuthorizeSectionAsync(Permissions.Assessments.Manage, command.CourseSectionId, openSemester: true, cancellationToken);
            if (section.IsFailure)
            {
                return Result<AssessmentSummary>.Fail(section.Errors);
            }

            var created = Assessment.Create(command.CourseSectionId, _clock.UtcNow);
            if (!created.TryGetValue(out var assessment))
            {
                return Result<AssessmentSummary>.Fail(created.Errors);
            }

            if (await _assessments.AnyAsync(new LiveAssessmentOfSectionSpecification(command.CourseSectionId), cancellationToken))
            {
                return AssessmentErrors.Exists;
            }

            _assessments.Add(assessment);

            return await CommitAsync(assessment, AssessmentAuditActions.Created, null, request, cancellationToken);
        }

        public async Task<Result<AssessmentSummary>> AddComponentAsync(Guid assessmentId, ComponentCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(assessmentId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<AssessmentSummary>.Fail(loaded.Errors);
            }

            var before = Describe(context.Assessment);
            var added = context.Assessment.AddComponent(command.Name, command.Type, command.Weight, command.MaxScore, command.DueDate, _clock.UtcNow);
            if (added.IsFailure)
            {
                return Result<AssessmentSummary>.Fail(added.Errors);
            }

            var dated = AssessmentAccess.CheckDueDate(context.Section.Semester, command.Type, command.DueDate);
            if (dated.IsFailure)
            {
                return Result<AssessmentSummary>.Fail(dated.Errors);
            }

            _assessments.Update(context.Assessment);

            return await CommitAsync(context.Assessment, AssessmentAuditActions.ComponentAdded, before, request, cancellationToken);
        }

        public async Task<Result<AssessmentSummary>> UpdateComponentAsync(Guid assessmentId, Guid componentId, ComponentCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(assessmentId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<AssessmentSummary>.Fail(loaded.Errors);
            }

            var before = Describe(context.Assessment);
            var updated = context.Assessment.UpdateComponent(componentId, command.Name, command.Type, command.Weight, command.MaxScore, command.DueDate, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<AssessmentSummary>.Fail(updated.Errors);
            }

            var dated = AssessmentAccess.CheckDueDate(context.Section.Semester, command.Type, command.DueDate);
            if (dated.IsFailure)
            {
                return Result<AssessmentSummary>.Fail(dated.Errors);
            }

            _assessments.Update(context.Assessment);

            return await CommitAsync(context.Assessment, AssessmentAuditActions.ComponentUpdated, before, request, cancellationToken);
        }

        public async Task<Result<AssessmentSummary>> RemoveComponentAsync(Guid assessmentId, Guid componentId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(assessmentId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<AssessmentSummary>.Fail(loaded.Errors);
            }

            var before = Describe(context.Assessment);
            var removed = context.Assessment.RemoveComponent(componentId, _clock.UtcNow);
            if (removed.IsFailure)
            {
                return Result<AssessmentSummary>.Fail(removed.Errors);
            }

            _assessments.Update(context.Assessment);

            return await CommitAsync(context.Assessment, AssessmentAuditActions.ComponentRemoved, before, request, cancellationToken);
        }

        public Task<Result<AssessmentSummary>> PublishAsync(Guid assessmentId, RequestInfo request, CancellationToken cancellationToken = default)
            => TransitionAsync(assessmentId, (a, now) => a.Publish(now), AssessmentAuditActions.Published, request, cancellationToken);

        public Task<Result<AssessmentSummary>> CloseAsync(Guid assessmentId, RequestInfo request, CancellationToken cancellationToken = default)
            => TransitionAsync(assessmentId, (a, now) => a.Close(now), AssessmentAuditActions.Closed, request, cancellationToken);

        public Task<Result<AssessmentSummary>> CancelAsync(Guid assessmentId, RequestInfo request, CancellationToken cancellationToken = default)
            => TransitionAsync(assessmentId, (a, now) => a.Cancel(now), AssessmentAuditActions.Cancelled, request, cancellationToken);

        private async Task<Result<AssessmentSummary>> TransitionAsync(Guid assessmentId, Func<Assessment, DateTimeOffset, Result> transition, string action, RequestInfo request, CancellationToken cancellationToken)
        {
            var loaded = await LoadForChangeAsync(assessmentId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<AssessmentSummary>.Fail(loaded.Errors);
            }

            var before = Describe(context.Assessment);
            var changed = transition(context.Assessment, _clock.UtcNow);
            if (changed.IsFailure)
            {
                return Result<AssessmentSummary>.Fail(changed.Errors);
            }

            _assessments.Update(context.Assessment);

            return await CommitAsync(context.Assessment, action, before, request, cancellationToken);
        }

        // ---------- read ----------

        public async Task<Result<AssessmentSummary>> GetAsync(Guid assessmentId, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Assessments.Read, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<AssessmentSummary>.Fail(permission.Errors);
            }

            var assessment = await _assessments.FirstOrDefaultAsync(new AssessmentByIdSpecification(assessmentId), cancellationToken);
            if (assessment is null)
            {
                return AssessmentErrors.NotFound;
            }

            var scope = await _access.AuthorizeSectionAsync(Permissions.Assessments.Read, assessment.CourseSectionId, openSemester: false, cancellationToken);

            return scope.IsFailure ? Result<AssessmentSummary>.Fail(scope.Errors) : AssessmentMapper.ToSummary(assessment);
        }

        // A section filter is checked against the section; without it only whole-university managers see the list.
        public async Task<Result<PagedResult<AssessmentSummary>>> ListAsync(PaginationQuery query, Guid? sectionId, AssessmentStatus? status, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Assessments.Read, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<PagedResult<AssessmentSummary>>.Fail(permission.Errors);
            }

            if (sectionId is { } section)
            {
                var scope = await _access.AuthorizeSectionAsync(Permissions.Assessments.Read, section, openSemester: false, cancellationToken);
                if (scope.IsFailure)
                {
                    return Result<PagedResult<AssessmentSummary>>.Fail(scope.Errors);
                }
            }
            else
            {
                var manager = await _access.RequireAsync(Permissions.Assessments.Manage, cancellationToken);
                if (manager.IsFailure)
                {
                    return Result<PagedResult<AssessmentSummary>>.Fail(manager.Errors);
                }
            }

            var items = await _assessments.ListAsync(new AssessmentsListSpecification(query, sectionId, status), cancellationToken);
            var total = await _assessments.CountAsync(new AssessmentsCountSpecification(sectionId, status), cancellationToken);

            return PagedResult<AssessmentSummary>.Create([.. items.Select(AssessmentMapper.ToSummary)], query, total);
        }
    }
}
