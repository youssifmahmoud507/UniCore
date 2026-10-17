using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Finance
{
    // Delivered by the outbox. The Notifications module will use these to tell the student.
    public sealed record FinancialHoldPlaced(Guid EventId, DateTimeOffset OccurredAt, Guid HoldId, Guid StudentId, FinancialHoldReason Reason, FinancialHoldArea Areas) : IDomainEvent;





}
