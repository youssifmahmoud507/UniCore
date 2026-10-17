using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record ScholarshipDto(Guid Id, string Code, string Name, string? Description, ReductionType Type, decimal Value, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);






}
