using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record UpdateDiscountCommand(string? Name, string? Description, ReductionType? Type, decimal? Value, DateOnly? ValidFrom, DateOnly? ValidTo);






}
