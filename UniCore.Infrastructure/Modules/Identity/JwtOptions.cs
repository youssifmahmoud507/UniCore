namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;

        // Secret. Comes from user-secrets / environment variables, never from a committed file.
        public string SigningKey { get; set; } = string.Empty;

        public int AccessTokenMinutes { get; set; } = 15;
        public int RefreshTokenDays { get; set; } = 7;
        public int ClockSkewSeconds { get; set; } = 30;
    }

}
