using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UniCore.Application.Modules.Identity;
using UniCore.Infrastructure.Common;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.IntegrationTests.Identity
{
    public class TokenServiceTests
    {
        private static readonly JwtOptions TestJwt = new()
        {
            Issuer = "UniCore.Tests",
            Audience = "UniCore.Tests.Api",
            SigningKey = "test-signing-key-test-signing-key-1234567890",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
            ClockSkewSeconds = 30
        };

        private static TokenService CreateService(JwtOptions? options = null)
            => new(Microsoft.Extensions.Options.Options.Create(options ?? TestJwt), new SystemClock());

        // Same validation rules as production, taken from the real configuration class.
        private static TokenValidationParameters ValidationParameters(JwtOptions options)
        {
            var bearer = new JwtBearerOptions();
            new ConfigureJwtBearerOptions(Microsoft.Extensions.Options.Options.Create(options)).Configure(bearer);
            return bearer.TokenValidationParameters;
        }

        [Fact]
        public async Task Access_token_round_trips_with_the_expected_claims()
        {
            var userId = Guid.NewGuid();
            var personId = Guid.NewGuid();
            var subject = new TokenSubject(userId, "ahmed", "ahmed@uni.edu", personId, new[] { "Student", "Instructor" });

            var token = CreateService().CreateAccessToken(subject);
            var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, ValidationParameters(TestJwt));

            Assert.True(validation.IsValid);
            var identity = validation.ClaimsIdentity;
            Assert.Equal(userId.ToString(), identity.FindFirst(AppClaimTypes.Sub)?.Value);
            Assert.Equal("ahmed@uni.edu", identity.FindFirst(AppClaimTypes.Email)?.Value);
            Assert.Equal("ahmed", identity.FindFirst(AppClaimTypes.Name)?.Value);
            Assert.Equal(personId.ToString(), identity.FindFirst(AppClaimTypes.PersonId)?.Value);
            Assert.Equal(2, identity.FindAll(AppClaimTypes.Role).Count());
            Assert.NotNull(identity.FindFirst(AppClaimTypes.Jti));
            Assert.InRange(token.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(14), DateTimeOffset.UtcNow.AddMinutes(16));
        }

        [Fact]
        public async Task Token_without_person_has_no_person_claim()
        {
            var subject = new TokenSubject(Guid.NewGuid(), "admin", "admin@uni.edu", null, Array.Empty<string>());

            var token = CreateService().CreateAccessToken(subject);
            var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, ValidationParameters(TestJwt));

            Assert.True(validation.IsValid);
            Assert.Null(validation.ClaimsIdentity.FindFirst(AppClaimTypes.PersonId));
        }

        [Fact]
        public async Task Token_signed_with_another_key_is_rejected()
        {
            var other = new JwtOptions
            {
                Issuer = TestJwt.Issuer,
                Audience = TestJwt.Audience,
                SigningKey = "a-completely-different-key-a-completely-different-key",
                AccessTokenMinutes = 15,
                RefreshTokenDays = 7,
                ClockSkewSeconds = 30
            };
            var subject = new TokenSubject(Guid.NewGuid(), "ahmed", "ahmed@uni.edu", null, Array.Empty<string>());

            var forged = CreateService(other).CreateAccessToken(subject);
            var validation = await new JsonWebTokenHandler().ValidateTokenAsync(forged.Token, ValidationParameters(TestJwt));

            Assert.False(validation.IsValid);
        }

        [Fact]
        public void Refresh_tokens_are_random_and_only_the_hash_is_derived_from_them()
        {
            var service = CreateService();

            var a = service.GenerateRefreshToken();
            var b = service.GenerateRefreshToken();

            Assert.NotEqual(a.Token, b.Token);
            Assert.NotEqual(a.TokenHash, b.TokenHash);
            Assert.Equal(service.HashRefreshToken(a.Token), a.TokenHash);
            Assert.NotEqual(a.Token, a.TokenHash);
            Assert.Equal(64, a.TokenHash.Length);
            Assert.InRange(a.ExpiresAt, DateTimeOffset.UtcNow.AddDays(7).AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(7).AddMinutes(1));
        }

        [Fact]
        public void Hashing_is_deterministic_sha256_hex()
        {
            Assert.Equal(
                "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",
                CreateService().HashRefreshToken("abc"));
        }
    }
}
