using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Notifications
{
    // A request started: the approvers of its first step are told.
    public sealed class WorkflowStartedNotificationHandler(WorkflowNotifier notifier) : IDomainEventHandler<WorkflowStarted>
    {
        private readonly WorkflowNotifier _notifier = notifier;

        public Task<Result> HandleAsync(WorkflowStarted domainEvent, CancellationToken cancellationToken = default)
            => _notifier.NotifyApproversAsync(domainEvent.InstanceId, domainEvent.NextRole, null, domainEvent.DefinitionCode, domainEvent.EventId, cancellationToken);
    }


}
