namespace UniCore.Api.Contracts.Finance
{
    public sealed record UpdateFeeDefinitionRequest(string? Name, string? Description, decimal? DefaultAmount);
}
