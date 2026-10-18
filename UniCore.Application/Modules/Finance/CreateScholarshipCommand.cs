using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record CreateScholarshipCommand(string? Code, string? Name, string? Description, ReductionType? Type, decimal? Value);






}
