using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents
{
    public sealed record DocumentAccessDto(Guid Id, Guid DocumentId, Guid UserId, DocumentAccessType AccessType, DocumentAccessStatus Status, DateTimeOffset GrantedAt, Guid? GrantedByUserId, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt);


}
