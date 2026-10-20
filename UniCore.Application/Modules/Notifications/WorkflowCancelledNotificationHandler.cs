using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Notifications
{
    // The requester knows when they cancel it themselves; they are told only when someone else did.
    public sealed class WorkflowCancelledNotificationHandler(WorkflowNotifier notifier) : IDomainEventHandler<WorkflowCancelled>
    {
        private readonly WorkflowNotifier _notifier = notifier;

        public Task<Result> HandleAsync(WorkflowCancelled domainEvent, CancellationToken cancellationToken = default)
            => domainEvent.CancelledByUserId == domainEvent.RequesterUserId
                ? Task.FromResult(Result.Ok())
                : _notifier.NotifyRequesterAsync(domainEvent.RequesterUserId, domainEvent.InstanceId, "Your request was cancelled", $"Your {WorkflowNotifier.Describe(domainEvent.DefinitionCode)} was cancelled by the administration.", domainEvent.EventId, cancellationToken);
    }


}
