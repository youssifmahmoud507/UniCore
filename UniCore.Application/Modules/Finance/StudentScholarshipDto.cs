using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record StudentScholarshipDto(Guid Id, Guid StudentId, Guid ScholarshipId, DateOnly ValidFrom, DateOnly? ValidTo, StudentScholarshipStatus Status, string? Reason, Guid? AwardedByUserId, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);






}
