using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Workflow.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Notifications
{
    // The two things the workflow events need: tell the approvers of a step, tell the requester of a result.
    public sealed class WorkflowNotifier(INotificationPublisher publisher, IRoleHolderFinder finder, IAggregateRepository<WorkflowInstance> instances)
    {
        public const string InstanceReferenceType = "WorkflowInstance";

        private readonly INotificationPublisher _publisher = publisher;
        private readonly IRoleHolderFinder _finder = finder;
        private readonly IAggregateRepository<WorkflowInstance> _instances = instances;

        // "WITHDRAWAL_REQUEST" becomes "withdrawal request".
        public static string Describe(string definitionCode) => definitionCode.Replace('_', ' ').ToLowerInvariant();

        // The users that hold the step's role where it covers the request's scope. The requester is never one of them.
        public async Task<Result> NotifyApproversAsync(Guid instanceId, string role, string? stepName, string definitionCode, Guid eventId, CancellationToken cancellationToken)
        {
            var instance = await _instances.FirstOrDefaultAsync(new WorkflowInstanceByIdSpecification(instanceId), cancellationToken);
            if (instance is null)
            {
                return Result.Ok();
            }

            var holders = await _finder.FindAsync(role, instance.ScopeType, instance.ScopeId, cancellationToken);
            if (!holders.TryGetValue(out var users))
            {
                return Result.Fail(holders.Errors);
            }

            var step = string.IsNullOrWhiteSpace(stepName) ? string.Empty : $" (step: {stepName})";

            foreach (var userId in users.Where(u => u != instance.RequesterUserId))
            {
                var sent = await _publisher.PublishAsync(
                    new NotificationRequest(userId, NotificationTypes.WorkflowActionRequired, "Request waiting for your approval", $"A {Describe(definitionCode)} is waiting for your approval{step}.", InstanceReferenceType, instanceId, eventId),
                    cancellationToken);

                if (sent.IsFailure)
                {
                    return Result.Fail(sent.Errors);
                }
            }

            return Result.Ok();
        }

        public async Task<Result> NotifyRequesterAsync(Guid requesterUserId, Guid instanceId, string title, string content, Guid eventId, CancellationToken cancellationToken)
        {
            var sent = await _publisher.PublishAsync(
                new NotificationRequest(requesterUserId, NotificationTypes.RequestStatusChanged, title, content, InstanceReferenceType, instanceId, eventId),
                cancellationToken);

            return sent.IsSuccess ? Result.Ok() : Result.Fail(sent.Errors);
        }
    }


}
