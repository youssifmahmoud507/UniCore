using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record CreateFeeDefinitionCommand(Guid UniversityId, string? Code, string? Name, string? Description, decimal? DefaultAmount, FeeBasis? Basis = null);



}
