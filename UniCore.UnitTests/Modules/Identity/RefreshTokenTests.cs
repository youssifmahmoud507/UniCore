using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    public class RefreshTokenTests
    {
        private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private static RefreshToken NewToken(DateTimeOffset? expiresAt = null, string? ip = "10.0.0.1")
        {
            var result = RefreshToken.Create(Guid.NewGuid(), "HASH", Now, expiresAt ?? Now.AddDays(7), ip);

            Assert.True(result.TryGetValue(out var token));
            Assert.NotNull(token);
            return token;
        }

        [Fact]
        public void Create_with_valid_data_gives_an_active_token()
        {
            var token = NewToken();

            Assert.NotEqual(Guid.Empty, token.Id);
            Assert.Equal("HASH", token.TokenHash);
            Assert.Equal(Now, token.CreatedAt);
            Assert.Equal("10.0.0.1", token.CreatedByIp);
            Assert.False(token.IsRevoked);
            Assert.True(token.IsActive(Now));
        }

        [Fact]
        public void Create_with_empty_user_fails()
        {
            var result = RefreshToken.Create(Guid.Empty, "HASH", Now, Now.AddDays(1), null);

            Assert.True(result.IsFailure);
            Assert.Equal("REFRESH_TOKEN_DATA_INVALID", result.Error?.Code);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_without_hash_fails(string? hash)
        {
            var result = RefreshToken.Create(Guid.NewGuid(), hash, Now, Now.AddDays(1), null);

            Assert.True(result.IsFailure);
            Assert.Equal("REFRESH_TOKEN_DATA_INVALID", result.Error?.Code);
        }

        [Fact]
        public void Create_with_expiry_not_after_now_fails()
        {
            var result = RefreshToken.Create(Guid.NewGuid(), "HASH", Now, Now, null);

            Assert.True(result.IsFailure);
        }

        [Fact]
        public void Token_expires_exactly_at_the_expiry_time()
        {
            var clock = new Common.FakeClock(Now);
            var token = NewToken(expiresAt: Now.AddMinutes(10));

            clock.Advance(TimeSpan.FromMinutes(10).Subtract(TimeSpan.FromSeconds(1)));
            Assert.True(token.IsActive(clock.UtcNow));

            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.True(token.IsExpired(clock.UtcNow));
            Assert.False(token.IsActive(clock.UtcNow));
        }

        [Fact]
        public void Revoke_marks_the_token_and_records_the_replacement()
        {
            var token = NewToken();
            var replacement = Guid.NewGuid();
            var later = Now.AddMinutes(5);

            var result = token.Revoke(later, "10.0.0.2", replacement);

            Assert.True(result.IsSuccess);
            Assert.True(token.IsRevoked);
            Assert.Equal(later, token.RevokedAt);
            Assert.Equal("10.0.0.2", token.RevokedByIp);
            Assert.Equal(replacement, token.ReplacedByTokenId);
            Assert.False(token.IsActive(later));
        }

        [Fact]
        public void Revoking_twice_fails_and_keeps_the_first_revocation()
        {
            var token = NewToken();
            var first = Now.AddMinutes(1);
            token.Revoke(first, "10.0.0.2");

            var second = token.Revoke(Now.AddMinutes(9), "10.0.0.3");

            Assert.True(second.IsFailure);
            Assert.Equal("REFRESH_TOKEN_ALREADY_REVOKED", second.Error?.Code);
            Assert.Equal(first, token.RevokedAt);
            Assert.Equal("10.0.0.2", token.RevokedByIp);
        }

        [Fact]
        public void Very_long_ip_values_are_truncated_to_45_characters()
        {
            var token = NewToken(ip: new string('1', 100));

            Assert.Equal(45, token.CreatedByIp?.Length);
        }
    }
}
