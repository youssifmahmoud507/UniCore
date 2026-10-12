namespace UniCore.Domain.Common.Events
{
    // Something that happened in the domain, named in the past tense (for example EnrollmentCreated).
    // Aggregates collect these; they are dispatched after the transaction commits (outbox, Week 21).
    public interface IDomainEvent
    {
        Guid EventId { get; }
        DateTimeOffset OccurredAt { get; }
    }
}
