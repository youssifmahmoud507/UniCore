using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Grading.Specifications;
using UniCore.Application.Modules.Workflow.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Grading
{
    // What happens to a grade change request when its GRADE_CHANGE workflow ends. Delivery is at-least-once, so every handler
    // does nothing when the request is no longer pending. Events of other workflows are ignored.
    public sealed class GradeChangeWorkflowHandlers(
        IAggregateRepository<GradeChangeRequest> requests, IAggregateRepository<WorkflowInstance> instances,
        IGradeCorrectionService corrections, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IAggregateRepository<GradeChangeRequest> _requests = requests;
        private readonly IAggregateRepository<WorkflowInstance> _instances = instances;
        private readonly IGradeCorrectionService _corrections = corrections;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static readonly RequestInfo Origin = new(null, "grade-change-workflow");

        private static bool IsMine(string referenceType) => string.Equals(referenceType, GradeChangeHandler.ReferenceType, StringComparison.Ordinal);

        private static bool IsPermanent(Error? error) => error?.ErrorType is ErrorType.Validation or ErrorType.NotFound or ErrorType.Conflict or ErrorType.BusinessRule or ErrorType.Forbidden or ErrorType.Unauthorized or ErrorType.InvalidCredentials;

        private async Task<GradeChangeRequest?> LoadPendingAsync(Guid id, CancellationToken cancellationToken)
        {
            var change = await _requests.FirstOrDefaultAsync(new GradeChangeByIdSpecification(id, tracking: true), cancellationToken);

            return change is { Status: GradeChangeRequestStatus.Pending } ? change : null;
        }

        private Task<Result> CommitAsync(string action, Guid? actor, GradeChangeRequest change, string? newValues, CancellationToken cancellationToken)
            => ChangeSupport.CommitAsync(_unitOfWork, _audit, new AuditEntry(action, actor, "GradeChangeRequest", change.Id.ToString(), null, Origin.UserAgent, null, newValues), cancellationToken);

        // The last approval is the final step: the person who made the correction official.
        public async Task<Result> CompletedAsync(WorkflowCompleted domainEvent, CancellationToken cancellationToken)
        {
            if (!IsMine(domainEvent.ReferenceType))
            {
                return Result.Ok();
            }

            var change = await LoadPendingAsync(domainEvent.ReferenceId, cancellationToken);
            if (change is null)
            {
                return Result.Ok();
            }

            var instance = await _instances.FirstOrDefaultAsync(new WorkflowInstanceByIdSpecification(domainEvent.InstanceId), cancellationToken);
            var approver = instance?.Approvals.Where(a => a.Decision == WorkflowDecision.Approved).OrderByDescending(a => a.DecidedAt).Select(a => (Guid?)a.ApproverUserId).FirstOrDefault();
            if (approver is not { } approvedBy)
            {
                return Result.Fail(GradeChangeErrors.NotFound);
            }

            var applied = await _corrections.ApplyApprovedCorrectionAsync(change.GradeId, change.AssessmentComponentId, change.NewScore, change.Reason, approvedBy, Origin, cancellationToken);
            if (applied.IsFailure)
            {
                // Retry only what may work next time (a concurrent change). A refused correction will be refused again: the request
                // is marked Failed so the people who asked can see it, and the event is done.
                if (!IsPermanent(applied.Error))
                {
                    return Result.Fail(applied.Errors);
                }

                if (change.MarkFailed(applied.Error?.Code ?? "GRADE_CHANGE_FAILED", _clock.UtcNow).IsFailure)
                {
                    return Result.Ok();
                }

                return await CommitAsync(GradeChangeAuditActions.Failed, approvedBy, change, $"Failed={change.FailureCode}", cancellationToken);
            }

            var marked = change.MarkApplied(approvedBy, _clock.UtcNow);
            if (marked.IsFailure)
            {
                return Result.Ok();
            }

            return await CommitAsync(GradeChangeAuditActions.Applied, approvedBy, change, $"Score={change.NewScore}", cancellationToken);
        }

        public async Task<Result> RejectedAsync(WorkflowRejected domainEvent, CancellationToken cancellationToken)
        {
            if (!IsMine(domainEvent.ReferenceType))
            {
                return Result.Ok();
            }

            var change = await LoadPendingAsync(domainEvent.ReferenceId, cancellationToken);
            if (change is null || change.MarkRejected(_clock.UtcNow).IsFailure)
            {
                return Result.Ok();
            }

            return await CommitAsync(GradeChangeAuditActions.Rejected, domainEvent.DecidedByUserId, change, $"Comment={domainEvent.Comment}", cancellationToken);
        }

        public async Task<Result> CancelledAsync(WorkflowCancelled domainEvent, CancellationToken cancellationToken)
        {
            if (!IsMine(domainEvent.ReferenceType))
            {
                return Result.Ok();
            }

            var change = await LoadPendingAsync(domainEvent.ReferenceId, cancellationToken);
            if (change is null || change.MarkCancelled(_clock.UtcNow).IsFailure)
            {
                return Result.Ok();
            }

            return await CommitAsync(GradeChangeAuditActions.Cancelled, domainEvent.CancelledByUserId, change, null, cancellationToken);
        }
    }

    public sealed class GradeChangeWorkflowCompletedHandler(GradeChangeWorkflowHandlers handlers) : IDomainEventHandler<WorkflowCompleted>
    {
        public Task<Result> HandleAsync(WorkflowCompleted domainEvent, CancellationToken cancellationToken = default) => handlers.CompletedAsync(domainEvent, cancellationToken);
    }

    public sealed class GradeChangeWorkflowRejectedHandler(GradeChangeWorkflowHandlers handlers) : IDomainEventHandler<WorkflowRejected>
    {
        public Task<Result> HandleAsync(WorkflowRejected domainEvent, CancellationToken cancellationToken = default) => handlers.RejectedAsync(domainEvent, cancellationToken);
    }

    public sealed class GradeChangeWorkflowCancelledHandler(GradeChangeWorkflowHandlers handlers) : IDomainEventHandler<WorkflowCancelled>
    {
        public Task<Result> HandleAsync(WorkflowCancelled domainEvent, CancellationToken cancellationToken = default) => handlers.CancelledAsync(domainEvent, cancellationToken);
    }
}
