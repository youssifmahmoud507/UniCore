using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Grading.Specifications;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Grading
{
    // The only way a Published or Locked grade changes: a request (GradeChanges.Request in the department of the grade's course)
    // starts the GRADE_CHANGE workflow (department head, then dean). When the workflow completes, GradeChangeWorkflowHandlers
    // apply the correction through IGradeCorrectionService. The workflow is approved or rejected through the workflow API.
    public sealed class GradeChangeHandler(
        GradingAccess access, ICurrentUser user, IAggregateRepository<Grade> grades, IAggregateRepository<GradeChangeRequest> requests,
        GradingEngine engine, IWorkflowService workflows, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        public const string WorkflowCode = "GRADE_CHANGE";
        public const string ReferenceType = "GradeChangeRequest";

        private readonly GradingAccess _access = access;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Grade> _grades = grades;
        private readonly IAggregateRepository<GradeChangeRequest> _requests = requests;
        private readonly GradingEngine _engine = engine;
        private readonly IWorkflowService _workflows = workflows;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result<GradeChangeDto>> RequestAsync(RequestGradeChangeCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var allowed = await _access.RequireAsync(Permissions.GradeChanges.Request, cancellationToken);
            if (allowed.IsFailure)
            {
                return Result<GradeChangeDto>.Fail(allowed.Errors);
            }

            if (command.GradeId is not { } gradeId || gradeId == Guid.Empty
                || command.AssessmentComponentId is not { } componentId || componentId == Guid.Empty
                || command.NewScore is not { } newScore)
            {
                return GradeChangeErrors.DataInvalid;
            }

            var grade = await _grades.FirstOrDefaultAsync(new GradeByIdSpecification(gradeId), cancellationToken);
            if (grade is null)
            {
                return GradingErrors.NotFound;
            }

            var authorized = await _access.AuthorizeSectionAsync(Permissions.GradeChanges.Request, grade.CourseSectionId, openSemester: false, cancellationToken);
            if (!authorized.TryGetValue(out var section))
            {
                return Result<GradeChangeDto>.Fail(authorized.Errors);
            }

            if (!grade.IsFinal)
            {
                return GradeChangeErrors.GradeNotFinal;
            }

            // Everything the final correction would reject is rejected now, so nobody approves a request that cannot be applied.
            var scheme = await _engine.LoadSchemeOfAssessmentAsync(grade.AssessmentId, cancellationToken);
            if (!scheme.TryGetValue(out var info))
            {
                return Result<GradeChangeDto>.Fail(scheme.Errors);
            }

            var component = info.Components.FirstOrDefault(c => c.ComponentId == componentId);
            var current = grade.Scores.FirstOrDefault(s => s.AssessmentComponentId == componentId);
            if (component is null || current is null)
            {
                return GradingErrors.CorrectionInvalid;
            }

            if (newScore < 0 || newScore > component.MaxScore)
            {
                return GradingErrors.ScoreInvalid;
            }

            if (await _requests.AnyAsync(new PendingGradeChangeOfGradeSpecification(gradeId), cancellationToken))
            {
                return GradeChangeErrors.AlreadyPending;
            }

            var created = GradeChangeRequest.Create(gradeId, grade.StudentId, grade.CourseSectionId, componentId, current.Score, newScore, command.Reason, userId, _clock.UtcNow);
            if (!created.TryGetValue(out var change))
            {
                return Result<GradeChangeDto>.Fail(created.Errors);
            }

            _requests.Add(change);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                // The filtered unique index lets one of two parallel requests for the same grade win.
                return saved.Error?.Code == CommonErrors.DuplicateResource.Code ? GradeChangeErrors.AlreadyPending : Result<GradeChangeDto>.Fail(saved.Errors);
            }

            var started = await _workflows.StartAuthorizedAsync(
                new StartWorkflowCommand(WorkflowCode, ReferenceType, change.Id, grade.StudentId, ScopeType.Department, section.DepartmentId), request, cancellationToken);

            if (!started.TryGetValue(out var instance))
            {
                // No workflow means nobody can approve it: close the request so it does not block the grade.
                var tracked = await _requests.FirstOrDefaultAsync(new GradeChangeByIdSpecification(change.Id, tracking: true), cancellationToken);
                if (tracked is not null && tracked.MarkCancelled(_clock.UtcNow).IsSuccess)
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return Result<GradeChangeDto>.Fail(started.Errors);
            }

            var trackedChange = await _requests.FirstOrDefaultAsync(new GradeChangeByIdSpecification(change.Id, tracking: true), cancellationToken);
            if (trackedChange is null)
            {
                return GradeChangeErrors.NotFound;
            }

            var attached = trackedChange.AttachWorkflow(instance.Id);
            if (attached.IsFailure)
            {
                return Result<GradeChangeDto>.Fail(attached.Errors);
            }

            var entry = new AuditEntry(GradeChangeAuditActions.Requested, userId, "GradeChangeRequest", trackedChange.Id.ToString(), request.IpAddress, request.UserAgent, $"Score={current.Score}", $"Score={newScore};Reason={trackedChange.Reason}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<GradeChangeDto>.Fail(committed.Errors) : GradeChangeMapper.ToDto(trackedChange);
        }

        public async Task<Result<GradeChangeDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForReadAsync(id, cancellationToken);

            return loaded.TryGetValue(out var change) ? GradeChangeMapper.ToDto(change) : Result<GradeChangeDto>.Fail(loaded.Errors);
        }

        public async Task<Result<IReadOnlyList<GradeChangeDto>>> ListOfGradeAsync(Guid gradeId, CancellationToken cancellationToken = default)
        {
            var allowed = await _access.RequireAsync(Permissions.Grades.Read, cancellationToken);
            if (allowed.IsFailure)
            {
                return Result<IReadOnlyList<GradeChangeDto>>.Fail(allowed.Errors);
            }

            var grade = await _grades.FirstOrDefaultAsync(new GradeByIdSpecification(gradeId), cancellationToken);
            if (grade is null)
            {
                return GradingErrors.NotFound;
            }

            var authorized = await AuthorizeReadAsync(grade.CourseSectionId, cancellationToken);
            if (authorized.IsFailure)
            {
                return Result<IReadOnlyList<GradeChangeDto>>.Fail(authorized.Errors);
            }

            var items = await _requests.ListAsync(new GradeChangesOfGradeSpecification(gradeId), cancellationToken);

            return items.Select(GradeChangeMapper.ToDto).ToList();
        }

        private async Task<Result<GradeChangeRequest>> LoadForReadAsync(Guid id, CancellationToken cancellationToken)
        {
            var change = await _requests.FirstOrDefaultAsync(new GradeChangeByIdSpecification(id), cancellationToken);
            if (change is null)
            {
                return GradeChangeErrors.NotFound;
            }

            var authorized = await AuthorizeReadAsync(change.CourseSectionId, cancellationToken);

            return authorized.IsFailure ? Result<GradeChangeRequest>.Fail(authorized.Errors) : change;
        }

        // Whoever may ask for a change or approve one, in the section or in its department, may read it.
        private async Task<Result> AuthorizeReadAsync(Guid sectionId, CancellationToken cancellationToken)
        {
            var asRequester = await _access.AuthorizeSectionAsync(Permissions.GradeChanges.Request, sectionId, openSemester: false, cancellationToken);
            if (asRequester.IsSuccess)
            {
                return Result.Ok();
            }

            var asApprover = await _access.AuthorizeSectionAsync(Permissions.GradeChanges.Approve, sectionId, openSemester: false, cancellationToken);

            return asApprover.IsSuccess ? Result.Ok() : Result.Fail(asRequester.Errors);
        }
    }
}
