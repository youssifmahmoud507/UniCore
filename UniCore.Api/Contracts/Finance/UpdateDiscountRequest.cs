using UniCore.Domain.Modules.Finance;

namespace UniCore.Api.Contracts.Finance
{
    public sealed record UpdateDiscountRequest(string? Name, string? Description, ReductionType? Type, decimal? Value, DateOnly? ValidFrom, DateOnly? ValidTo);
}
