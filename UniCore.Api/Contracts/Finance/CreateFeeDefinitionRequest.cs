namespace UniCore.Api.Contracts.Finance
{
    public sealed record CreateFeeDefinitionRequest(Guid? UniversityId, string? Code, string? Name, string? Description, decimal? DefaultAmount);
}
