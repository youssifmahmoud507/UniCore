using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Notifications
{
    public sealed class WorkflowReturnedNotificationHandler(WorkflowNotifier notifier) : IDomainEventHandler<WorkflowReturned>
    {
        private readonly WorkflowNotifier _notifier = notifier;

        public Task<Result> HandleAsync(WorkflowReturned domainEvent, CancellationToken cancellationToken = default)
            => _notifier.NotifyRequesterAsync(domainEvent.RequesterUserId, domainEvent.InstanceId, "Your request needs a correction", $"Your {WorkflowNotifier.Describe(domainEvent.DefinitionCode)} was sent back. Note: {domainEvent.Comment}", domainEvent.EventId, cancellationToken);
    }


}
