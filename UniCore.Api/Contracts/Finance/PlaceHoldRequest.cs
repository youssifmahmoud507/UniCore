using UniCore.Domain.Modules.Finance;

namespace UniCore.Api.Contracts.Finance
{
    // Areas: any of "Registration", "Graduation", "Services".
    public sealed record PlaceHoldRequest(Guid? StudentId, List<FinancialHoldArea>? Areas, string? Note);
}
