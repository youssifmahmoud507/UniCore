using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.IntegrationTests.Identity
{
    public class JwtOptionsValidatorTests
    {
        private readonly JwtOptionsValidator _validator = new();

        private static JwtOptions Valid() => new()
        {
            Issuer = "issuer",
            Audience = "audience",
            SigningKey = new string('k', 32),
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
            ClockSkewSeconds = 30
        };

        [Fact]
        public void Valid_options_pass()
            => Assert.True(_validator.Validate(null, Valid()).Succeeded);

        [Fact]
        public void Short_signing_key_fails_without_leaking_the_key()
        {
            var options = Valid();
            options.SigningKey = "too-short-secret";

            var result = _validator.Validate(null, options);

            Assert.True(result.Failed);
            Assert.DoesNotContain("too-short-secret", string.Join(" ", result.Failures ?? Array.Empty<string>()));
        }

        [Fact]
        public void Missing_issuer_or_audience_fails()
        {
            var noIssuer = Valid();
            noIssuer.Issuer = "";
            var noAudience = Valid();
            noAudience.Audience = " ";

            Assert.True(_validator.Validate(null, noIssuer).Failed);
            Assert.True(_validator.Validate(null, noAudience).Failed);
        }

        [Theory]
        [InlineData(0, 7, 30)]
        [InlineData(15, 0, 30)]
        [InlineData(15, 7, -1)]
        public void Non_positive_lifetimes_fail(int minutes, int days, int skew)
        {
            var options = Valid();
            options.AccessTokenMinutes = minutes;
            options.RefreshTokenDays = days;
            options.ClockSkewSeconds = skew;

            Assert.True(_validator.Validate(null, options).Failed);
        }
    }
}
