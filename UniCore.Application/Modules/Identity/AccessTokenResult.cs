namespace UniCore.Application.Modules.Identity
{
    public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);
}
