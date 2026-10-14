using UniCore.Domain.Common.Events;
using UniCore.Domain.Common.Results;

namespace UniCore.Application.Common.Abstractions
{
    // Reacts to a domain event after the transaction that raised it has committed (delivered by the outbox).
    // Delivery is at-least-once, so a handler must be idempotent.
    public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
    {
        Task<Result> HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
    }
}


