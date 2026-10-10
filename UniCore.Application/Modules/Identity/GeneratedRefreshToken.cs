namespace UniCore.Application.Modules.Identity
{
    // Token is the raw value for the client. TokenHash is what gets stored.
    public sealed record GeneratedRefreshToken(string Token, string TokenHash, DateTimeOffset ExpiresAt);
}
