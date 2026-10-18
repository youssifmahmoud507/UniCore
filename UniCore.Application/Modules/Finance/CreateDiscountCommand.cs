using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record CreateDiscountCommand(string? Code, string? Name, string? Description, ReductionType? Type, decimal? Value, DateOnly? ValidFrom, DateOnly? ValidTo);






}
