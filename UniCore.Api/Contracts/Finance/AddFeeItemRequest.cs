namespace UniCore.Api.Contracts.Finance
{
    // Amount = null means "use the fee definition's default amount".
    public sealed record AddFeeItemRequest(Guid? FeeDefinitionId, decimal? Amount);
}
