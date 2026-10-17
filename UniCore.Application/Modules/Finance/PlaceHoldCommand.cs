using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record PlaceHoldCommand(Guid? StudentId, IReadOnlyList<FinancialHoldArea>? Areas, string? Note);




}
