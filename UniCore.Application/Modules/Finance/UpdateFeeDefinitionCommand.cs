namespace UniCore.Application.Modules.Finance
{
    public sealed record UpdateFeeDefinitionCommand(string? Name, string? Description, decimal? DefaultAmount);

}
