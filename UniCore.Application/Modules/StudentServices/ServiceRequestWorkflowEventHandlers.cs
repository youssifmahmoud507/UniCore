using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.StudentServices
{
    // The four subscriptions: the engine's events -> ServiceRequestWorkflowReactions.
    public sealed class ServiceRequestWorkflowCompletedHandler(ServiceRequestWorkflowReactions reactions) : IDomainEventHandler<WorkflowCompleted>
    {
        public Task<Result> HandleAsync(WorkflowCompleted domainEvent, CancellationToken cancellationToken = default) => reactions.OnCompletedAsync(domainEvent, cancellationToken);
    }

    public sealed class ServiceRequestWorkflowRejectedHandler(ServiceRequestWorkflowReactions reactions) : IDomainEventHandler<WorkflowRejected>
    {
        public Task<Result> HandleAsync(WorkflowRejected domainEvent, CancellationToken cancellationToken = default) => reactions.OnRejectedAsync(domainEvent, cancellationToken);
    }

    public sealed class ServiceRequestWorkflowReturnedHandler(ServiceRequestWorkflowReactions reactions) : IDomainEventHandler<WorkflowReturned>
    {
        public Task<Result> HandleAsync(WorkflowReturned domainEvent, CancellationToken cancellationToken = default) => reactions.OnReturnedAsync(domainEvent, cancellationToken);
    }

    public sealed class ServiceRequestWorkflowCancelledHandler(ServiceRequestWorkflowReactions reactions) : IDomainEventHandler<WorkflowCancelled>
    {
        public Task<Result> HandleAsync(WorkflowCancelled domainEvent, CancellationToken cancellationToken = default) => reactions.OnCancelledAsync(domainEvent, cancellationToken);
    }
}
