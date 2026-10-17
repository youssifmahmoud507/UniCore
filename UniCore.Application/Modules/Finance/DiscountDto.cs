using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record DiscountDto(Guid Id, string Code, string Name, string? Description, ReductionType Type, decimal Value, DateOnly? ValidFrom, DateOnly? ValidTo, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);






}
