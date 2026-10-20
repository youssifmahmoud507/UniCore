using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Notifications
{
    public sealed class WorkflowCompletedNotificationHandler(WorkflowNotifier notifier) : IDomainEventHandler<WorkflowCompleted>
    {
        private readonly WorkflowNotifier _notifier = notifier;

        public Task<Result> HandleAsync(WorkflowCompleted domainEvent, CancellationToken cancellationToken = default)
            => _notifier.NotifyRequesterAsync(domainEvent.RequesterUserId, domainEvent.InstanceId, "Your request was approved", $"Your {WorkflowNotifier.Describe(domainEvent.DefinitionCode)} was approved.", domainEvent.EventId, cancellationToken);
    }


}
