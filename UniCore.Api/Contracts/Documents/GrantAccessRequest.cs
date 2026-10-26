using UniCore.Domain.Modules.Documents;

namespace UniCore.Api.Contracts.Documents
{
    // AccessType: "Read" (default) or "Manage". ExpiresAt = null never expires.
    public sealed record GrantAccessRequest(Guid? UserId, DocumentAccessType? AccessType, DateTimeOffset? ExpiresAt);

}
