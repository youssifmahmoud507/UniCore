using UniCore.Domain.Modules.Finance;

namespace UniCore.Api.Contracts.Finance
{
    public sealed record UpdateScholarshipRequest(string? Name, string? Description, ReductionType? Type, decimal? Value);
}
