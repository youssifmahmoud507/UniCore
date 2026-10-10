using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Identity
{
    // Only a hash of the OTP is stored, never the OTP itself.
    public sealed class PasswordResetOtp
    {
        private const int MaxIpLength = 45;

        private PasswordResetOtp()
        {
        }

        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string OtpHash { get; private set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; private set; }
        public int AttemptCount { get; private set; }
        public int MaxAttempts { get; private set; }
        public DateTimeOffset? ConsumedAt { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public string? RequestedByIp { get; private set; }
        public DateTimeOffset ResendAvailableAt { get; private set; }

        public bool IsConsumed => ConsumedAt is not null;
        public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
        public bool IsLocked => AttemptCount >= MaxAttempts;
        public bool IsUsable(DateTimeOffset now) => !IsConsumed && !IsExpired(now) && !IsLocked;

        public static Result<PasswordResetOtp> Create(Guid userId,string? otpHash,DateTimeOffset now,TimeSpan lifetime,int maxAttempts,TimeSpan resendCooldown,string? requestedByIp)
        {
            if (userId == Guid.Empty || string.IsNullOrWhiteSpace(otpHash) || lifetime <= TimeSpan.Zero || maxAttempts <= 0 || resendCooldown < TimeSpan.Zero)
            {
                return IdentityErrors.OtpDataInvalid;
            }

            var ip = requestedByIp?.Trim();

            return new PasswordResetOtp
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OtpHash = otpHash.Trim(),
                CreatedAt = now,
                ExpiresAt = now.Add(lifetime),
                MaxAttempts = maxAttempts,
                ResendAvailableAt = now.Add(resendCooldown),
                RequestedByIp = string.IsNullOrEmpty(ip) ? null : (ip.Length <= MaxIpLength ? ip : ip[..MaxIpLength])
            };
        }

        // Single use: fails when consumed, expired or locked.
        public Result Consume(DateTimeOffset now)
        {
            if (!IsUsable(now))
            {
                return Result.Fail(IdentityErrors.OtpInvalid);
            }

            ConsumedAt = now;
            return Result.Ok();
        }

        // Idempotent. Used when a newer OTP replaces this one, and after a password reset.
        public void Invalidate(DateTimeOffset now) => ConsumedAt ??= now;

        // Returns true when this failure locked the OTP.
        public bool RegisterFailedAttempt()
        {
            if (!IsLocked)
            {
                AttemptCount++;
            }

            return IsLocked;
        }
    }
}