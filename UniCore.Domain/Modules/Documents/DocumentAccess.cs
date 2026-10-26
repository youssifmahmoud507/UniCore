using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Documents
{
    // Permission for one person to use one document, with an optional end time. Only one active grant per
    // (document, person). Revoking is a status change, so the history stays.
    public sealed class DocumentAccess : AggregateRoot
    {
        private DocumentAccess()
        {
        }

        public Guid DocumentId { get; private set; }
        public Guid UserId { get; private set; }
        public DocumentAccessType AccessType { get; private set; }
        public DocumentAccessStatus Status { get; private set; }
        public DateTimeOffset GrantedAt { get; private set; }
        public Guid? GrantedByUserId { get; private set; }
        public DateTimeOffset? ExpiresAt { get; private set; }
        public DateTimeOffset? RevokedAt { get; private set; }
        public Guid? RevokedByUserId { get; private set; }

        public static Result<DocumentAccess> Grant(Guid documentId, Guid userId, DocumentAccessType accessType, DateTimeOffset? expiresAt, Guid? grantedByUserId, DateTimeOffset now)
        {
            if (documentId == Guid.Empty
                || userId == Guid.Empty
                || !Enum.IsDefined(accessType)
                || (expiresAt is { } end && end <= now))
            {
                return DocumentErrors.AccessInvalid;
            }

            return new DocumentAccess
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                UserId = userId,
                AccessType = accessType,
                Status = DocumentAccessStatus.Active,
                GrantedAt = now,
                GrantedByUserId = grantedByUserId,
                ExpiresAt = expiresAt
            };
        }

        public Result Revoke(Guid? revokedByUserId, DateTimeOffset now)
        {
            if (Status != DocumentAccessStatus.Active)
            {
                return Result.Fail(DocumentErrors.AccessNotActive);
            }

            Status = DocumentAccessStatus.Revoked;
            RevokedAt = now;
            RevokedByUserId = revokedByUserId;

            return Result.Ok();
        }

        public bool IsValidAt(DateTimeOffset moment) => Status == DocumentAccessStatus.Active && (ExpiresAt is null || ExpiresAt > moment);
    }



}
