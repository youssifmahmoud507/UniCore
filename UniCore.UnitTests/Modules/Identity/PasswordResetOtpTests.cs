using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    public class PasswordResetOtpTests
    {
        private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private static PasswordResetOtp NewOtp(int maxAttempts = 3)
        {
            var result = PasswordResetOtp.Create(
                Guid.NewGuid(), "HASH", Now, TimeSpan.FromMinutes(10), maxAttempts, TimeSpan.FromSeconds(60), "10.0.0.1");

            Assert.True(result.TryGetValue(out var otp));
            Assert.NotNull(otp);
            return otp;
        }

        [Fact]
        public void Create_sets_expiry_cooldown_and_starts_usable()
        {
            var otp = NewOtp();

            Assert.Equal(Now.AddMinutes(10), otp.ExpiresAt);
            Assert.Equal(Now.AddSeconds(60), otp.ResendAvailableAt);
            Assert.Equal(0, otp.AttemptCount);
            Assert.True(otp.IsUsable(Now));
        }

        [Fact]
        public void Create_with_invalid_data_fails()
        {
            var noUser = PasswordResetOtp.Create(Guid.Empty, "H", Now, TimeSpan.FromMinutes(1), 5, TimeSpan.Zero, null);
            var noHash = PasswordResetOtp.Create(Guid.NewGuid(), " ", Now, TimeSpan.FromMinutes(1), 5, TimeSpan.Zero, null);
            var noLifetime = PasswordResetOtp.Create(Guid.NewGuid(), "H", Now, TimeSpan.Zero, 5, TimeSpan.Zero, null);
            var noAttempts = PasswordResetOtp.Create(Guid.NewGuid(), "H", Now, TimeSpan.FromMinutes(1), 0, TimeSpan.Zero, null);

            Assert.All(new[] { noUser, noHash, noLifetime, noAttempts }, r =>
            {
                Assert.True(r.IsFailure);
                Assert.Equal("OTP_DATA_INVALID", r.Error?.Code);
            });
        }

        [Fact]
        public void Otp_expires_exactly_at_the_expiry_time()
        {
            var otp = NewOtp();

            Assert.True(otp.IsUsable(Now.AddMinutes(10).AddTicks(-1)));
            Assert.False(otp.IsUsable(Now.AddMinutes(10)));
            Assert.True(otp.IsExpired(Now.AddMinutes(10)));
        }

        [Fact]
        public void Failed_attempts_lock_the_otp_at_the_maximum()
        {
            var otp = NewOtp(maxAttempts: 3);

            Assert.False(otp.RegisterFailedAttempt());
            Assert.False(otp.RegisterFailedAttempt());
            Assert.True(otp.RegisterFailedAttempt());

            Assert.True(otp.IsLocked);
            Assert.False(otp.IsUsable(Now));
            Assert.Equal(3, otp.AttemptCount);

            otp.RegisterFailedAttempt();
            Assert.Equal(3, otp.AttemptCount);
        }

        [Fact]
        public void Consume_is_single_use()
        {
            var otp = NewOtp();

            Assert.True(otp.Consume(Now).IsSuccess);
            Assert.True(otp.IsConsumed);

            var second = otp.Consume(Now);
            Assert.True(second.IsFailure);
            Assert.Equal("OTP_INVALID", second.Error?.Code);
        }

        [Fact]
        public void Consume_fails_when_expired_or_locked()
        {
            var expired = NewOtp();
            Assert.True(expired.Consume(Now.AddMinutes(11)).IsFailure);

            var locked = NewOtp(maxAttempts: 1);
            locked.RegisterFailedAttempt();
            Assert.True(locked.Consume(Now).IsFailure);
        }

        [Fact]
        public void Invalidate_is_idempotent_and_keeps_the_first_time()
        {
            var otp = NewOtp();

            otp.Invalidate(Now.AddMinutes(1));
            otp.Invalidate(Now.AddMinutes(5));

            Assert.Equal(Now.AddMinutes(1), otp.ConsumedAt);
            Assert.False(otp.IsUsable(Now.AddMinutes(2)));
        }
    }



}
