using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Notifications
{
    public sealed class WorkflowRejectedNotificationHandler(WorkflowNotifier notifier) : IDomainEventHandler<WorkflowRejected>
    {
        private readonly WorkflowNotifier _notifier = notifier;

        public Task<Result> HandleAsync(WorkflowRejected domainEvent, CancellationToken cancellationToken = default)
            => _notifier.NotifyRequesterAsync(domainEvent.RequesterUserId, domainEvent.InstanceId, "Your request was rejected", $"Your {WorkflowNotifier.Describe(domainEvent.DefinitionCode)} was rejected. Reason: {domainEvent.Comment}", domainEvent.EventId, cancellationToken);
    }


}
