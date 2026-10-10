using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Identity
{
    // Only the hash of the token is stored. The raw token is shown to the client once.
    public sealed class RefreshToken
    {
        private const int MaxIpLength = 45; // longest textual IPv6 address

        private RefreshToken()
        {
        }

        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string TokenHash { get; private set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? RevokedAt { get; private set; }
        public Guid? ReplacedByTokenId { get; private set; }
        public string? CreatedByIp { get; private set; }
        public string? RevokedByIp { get; private set; }

        public bool IsRevoked => RevokedAt is not null;
        public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
        public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

        public static Result<RefreshToken> Create(
            Guid userId, string? tokenHash, DateTimeOffset now, DateTimeOffset expiresAt, string? createdByIp)
        {
            if (userId == Guid.Empty || string.IsNullOrWhiteSpace(tokenHash) || expiresAt <= now)
            {
                return IdentityErrors.RefreshTokenDataInvalid;
            }

            // The Id is set here (not by EF) because a rotation needs the new token's Id
            // to fill ReplacedByTokenId on the old token before anything is saved.
            return new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = tokenHash.Trim(),
                CreatedAt = now,
                ExpiresAt = expiresAt,
                CreatedByIp = NormalizeIp(createdByIp)
            };
        }

        public Result Revoke(DateTimeOffset now, string? revokedByIp, Guid? replacedByTokenId = null)
        {
            if (IsRevoked)
            {
                return Result.Fail(IdentityErrors.RefreshTokenAlreadyRevoked);
            }

            RevokedAt = now;
            RevokedByIp = NormalizeIp(revokedByIp);
            ReplacedByTokenId = replacedByTokenId;
            return Result.Ok();
        }

        private static string? NormalizeIp(string? ip)
        {
            var clean = ip?.Trim();

            if (string.IsNullOrEmpty(clean))
            {
                return null;
            }

            return clean.Length <= MaxIpLength ? clean : clean[..MaxIpLength];
        }
    }
}
