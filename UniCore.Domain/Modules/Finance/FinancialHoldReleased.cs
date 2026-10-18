using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Finance
{
    public sealed record FinancialHoldReleased(Guid EventId, DateTimeOffset OccurredAt, Guid HoldId, Guid StudentId, bool ReleasedAutomatically) : IDomainEvent;





}
