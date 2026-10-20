using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Notifications
{
    // The request reached a new step: the approvers of that step are told, and so is the requester.
    public sealed class WorkflowStepAdvancedNotificationHandler(WorkflowNotifier notifier) : IDomainEventHandler<WorkflowStepAdvanced>
    {
        private readonly WorkflowNotifier _notifier = notifier;

        public async Task<Result> HandleAsync(WorkflowStepAdvanced domainEvent, CancellationToken cancellationToken = default)
        {
            var approvers = await _notifier.NotifyApproversAsync(domainEvent.InstanceId, domainEvent.RequiredRole, domainEvent.StepName, domainEvent.DefinitionCode, domainEvent.EventId, cancellationToken);
            if (approvers.IsFailure)
            {
                return approvers;
            }

            return await _notifier.NotifyRequesterAsync(
                domainEvent.RequesterUserId,
                domainEvent.InstanceId,
                "Your request moved on",
                $"Your {WorkflowNotifier.Describe(domainEvent.DefinitionCode)} moved to the next step ({domainEvent.StepName}).",
                domainEvent.EventId,
                cancellationToken);
        }
    }


}
