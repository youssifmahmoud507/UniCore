using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record FinancialHoldDto(Guid Id, Guid StudentId, FinancialHoldReason Reason, IReadOnlyList<FinancialHoldArea> Areas, FinancialHoldStatus Status, decimal OverdueAmount, string Note, Guid? PlacedByUserId, DateTimeOffset PlacedAt, DateTimeOffset? ReleasedAt, Guid? ReleasedByUserId, string? ReleaseNote);




}
