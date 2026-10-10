namespace UniCore.Application.Modules.Identity
{
    public sealed record AuthTokensResponse(string AccessToken,DateTimeOffset AccessTokenExpiresAt,string RefreshToken,DateTimeOffset RefreshTokenExpiresAt)
    {
        public string TokenType => "Bearer";
    }
}
