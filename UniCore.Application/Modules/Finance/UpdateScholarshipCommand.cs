using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record UpdateScholarshipCommand(string? Name, string? Description, ReductionType? Type, decimal? Value);






}
