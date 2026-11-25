using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.StudentServices.Specifications;
using UniCore.Application.Modules.Transcripts;
using UniCore.Application.Modules.Workflow.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.StudentServices;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.StudentServices
{
    // The request mirrors the outcome of its workflow. The engine publishes WorkflowCompleted / Rejected / Returned / Cancelled with the
    // ReferenceType and ReferenceId it was started with; the four kinds recognise their own ReferenceType and ignore the others.
    //
    // The outbox delivers at least once, so each reaction is safe to repeat:
    //  - the request is already in the target state -> done;
    //  - the event is not for the request's current workflow, or older than the request's last change (a late copy of an event
    //    from before a resubmit) -> ignored;
    //  - the request is no longer Pending (cancelled meanwhile) -> ignored.
    public sealed class ServiceRequestWorkflowReactions(
        IAggregateRepository<CertificateRequest> certificates, IAggregateRepository<TranscriptRequest> transcripts,
        IAggregateRepository<Complaint> complaints, IAggregateRepository<Appeal> appeals,
        IAggregateRepository<WorkflowInstance> instances, ITranscriptSnapshotService snapshots,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IAggregateRepository<CertificateRequest> _certificates = certificates;
        private readonly IAggregateRepository<TranscriptRequest> _transcripts = transcripts;
        private readonly IAggregateRepository<Complaint> _complaints = complaints;
        private readonly IAggregateRepository<Appeal> _appeals = appeals;
        private readonly IAggregateRepository<WorkflowInstance> _instances = instances;
        private readonly ITranscriptSnapshotService _snapshots = snapshots;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result> OnCompletedAsync(WorkflowCompleted e, CancellationToken cancellationToken)
        {
            var decidedBy = await ApproverOfAsync(e.InstanceId, e.RequesterUserId, cancellationToken);

            return e.ReferenceType switch
            {
                ServiceRequestKinds.CertificateReference => await ApplyAsync(_certificates, e.ReferenceId, e.InstanceId, e.OccurredAt, ServiceRequestStatus.Approved,
                    r => r.Approve(decidedBy, _clock.UtcNow), ServiceRequestAuditActions.Approved, decidedBy, cancellationToken),
                ServiceRequestKinds.TranscriptReference => await CompleteTranscriptAsync(e, decidedBy, cancellationToken),
                ServiceRequestKinds.ComplaintReference => await ApplyAsync(_complaints, e.ReferenceId, e.InstanceId, e.OccurredAt, ServiceRequestStatus.Approved,
                    r => r.Approve(decidedBy, _clock.UtcNow), ServiceRequestAuditActions.Approved, decidedBy, cancellationToken),
                ServiceRequestKinds.AppealReference => await ApplyAsync(_appeals, e.ReferenceId, e.InstanceId, e.OccurredAt, ServiceRequestStatus.Approved,
                    r => r.Approve(decidedBy, _clock.UtcNow), ServiceRequestAuditActions.Approved, decidedBy, cancellationToken),
                _ => Result.Ok(),
            };
        }

        public Task<Result> OnRejectedAsync(WorkflowRejected e, CancellationToken cancellationToken)
            => DecideAsync(e.ReferenceType, e.ReferenceId, e.InstanceId, e.OccurredAt, ServiceRequestStatus.Rejected,
                r => r.Reject(e.DecidedByUserId, e.Comment, _clock.UtcNow), ServiceRequestAuditActions.Rejected, e.DecidedByUserId, cancellationToken);

        public Task<Result> OnReturnedAsync(WorkflowReturned e, CancellationToken cancellationToken)
            => DecideAsync(e.ReferenceType, e.ReferenceId, e.InstanceId, e.OccurredAt, ServiceRequestStatus.Returned,
                r => r.Return(e.DecidedByUserId, e.Comment, _clock.UtcNow), ServiceRequestAuditActions.Returned, e.DecidedByUserId, cancellationToken);

        public Task<Result> OnCancelledAsync(WorkflowCancelled e, CancellationToken cancellationToken)
            => DecideAsync(e.ReferenceType, e.ReferenceId, e.InstanceId, e.OccurredAt, ServiceRequestStatus.Cancelled,
                r => r.Cancel(e.CancelledByUserId, _clock.UtcNow), ServiceRequestAuditActions.Cancelled, e.CancelledByUserId, cancellationToken);

        private async Task<Result> DecideAsync(
            string referenceType, Guid id, Guid instanceId, DateTimeOffset occurredAt, ServiceRequestStatus target,
            Func<ServiceRequest, Result> change, string action, Guid? actor, CancellationToken cancellationToken)
            => referenceType switch
            {
                ServiceRequestKinds.CertificateReference => await ApplyAsync(_certificates, id, instanceId, occurredAt, target, change, action, actor, cancellationToken),
                ServiceRequestKinds.TranscriptReference => await ApplyAsync(_transcripts, id, instanceId, occurredAt, target, change, action, actor, cancellationToken),
                ServiceRequestKinds.ComplaintReference => await ApplyAsync(_complaints, id, instanceId, occurredAt, target, change, action, actor, cancellationToken),
                ServiceRequestKinds.AppealReference => await ApplyAsync(_appeals, id, instanceId, occurredAt, target, change, action, actor, cancellationToken),
                _ => Result.Ok(),
            };

        private async Task<Result> ApplyAsync<T>(
            IAggregateRepository<T> repository, Guid id, Guid instanceId, DateTimeOffset occurredAt, ServiceRequestStatus target,
            Func<T, Result> change, string action, Guid? actor, CancellationToken cancellationToken) where T : ServiceRequest
        {
            var request = await repository.FirstOrDefaultAsync(new ServiceRequestByIdSpecification<T>(id, tracking: true), cancellationToken);

            return await ApplyToAsync(repository, request, instanceId, occurredAt, target, change, action, actor, cancellationToken);
        }

        private async Task<Result> ApplyToAsync<T>(
            IAggregateRepository<T> repository, T? request, Guid instanceId, DateTimeOffset occurredAt, ServiceRequestStatus target,
            Func<T, Result> change, string action, Guid? actor, CancellationToken cancellationToken) where T : ServiceRequest
        {
            if (request is null || request.Status == target)
            {
                return Result.Ok();
            }

            if (request.WorkflowInstanceId is { } attached && attached != instanceId)
            {
                return Result.Ok();
            }

            if (occurredAt < request.UpdatedAt || request.Status != ServiceRequestStatus.Pending)
            {
                return Result.Ok();
            }

            var changed = change(request);
            if (changed.IsFailure)
            {
                return changed;
            }

            repository.Update(request);

            var entry = new AuditEntry(action, actor, request.GetType().Name, request.Id.ToString(), null, null, $"Status={ServiceRequestStatus.Pending}", $"Status={request.Status}");

            return await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
        }

        // Approved -> the official snapshot is issued. Retried by the outbox until it works (a financial hold, for example, can block it):
        // the approval is kept, and the next delivery only repeats the issuing.
        private async Task<Result> CompleteTranscriptAsync(WorkflowCompleted e, Guid decidedBy, CancellationToken cancellationToken)
        {
            var request = await _transcripts.FirstOrDefaultAsync(new ServiceRequestByIdSpecification<TranscriptRequest>(e.ReferenceId, tracking: true), cancellationToken);
            if (request is null)
            {
                return Result.Ok();
            }

            var approved = await ApplyToAsync(_transcripts, request, e.InstanceId, e.OccurredAt, ServiceRequestStatus.Approved,
                r => r.Approve(decidedBy, _clock.UtcNow), ServiceRequestAuditActions.Approved, decidedBy, cancellationToken);
            if (approved.IsFailure || request.Status != ServiceRequestStatus.Approved || request.IsIssued)
            {
                return approved;
            }

            var issued = await _snapshots.IssueOfficialAsync(request.StudentId, decidedBy, new RequestInfo(null, null), cancellationToken);
            if (!issued.TryGetValue(out var transcript) || transcript.SnapshotId is not { } snapshotId)
            {
                return issued.IsFailure ? Result.Fail(issued.Errors) : Result.Fail(ServiceRequestErrors.InvalidState);
            }

            var recorded = request.RecordIssued(snapshotId, _clock.UtcNow);
            if (recorded.IsFailure)
            {
                return recorded;
            }

            _transcripts.Update(request);

            var entry = new AuditEntry(ServiceRequestAuditActions.TranscriptIssued, decidedBy, nameof(TranscriptRequest), request.Id.ToString(), null, null, null, $"SnapshotId={snapshotId}");

            return await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
        }

        // The workflow has no "decided by" on its Completed event: the last approval in the instance's log is the approver.
        // Falls back to the requester when the instance cannot be read, so the audit entry always has an actor.
        private async Task<Guid> ApproverOfAsync(Guid instanceId, Guid fallback, CancellationToken cancellationToken)
        {
            var instance = await _instances.FirstOrDefaultAsync(new WorkflowInstanceByIdSpecification(instanceId), cancellationToken);
            var last = instance?.Actions.Where(a => a.ActionType == WorkflowActionType.Approved).OrderByDescending(a => a.PerformedAt).FirstOrDefault();

            return last?.PerformedByUserId ?? fallback;
        }
    }
}
