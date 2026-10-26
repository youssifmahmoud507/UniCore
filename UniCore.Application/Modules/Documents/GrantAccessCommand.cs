using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents
{
    public sealed record GrantAccessCommand(Guid? UserId, DocumentAccessType? AccessType, DateTimeOffset? ExpiresAt);


}
