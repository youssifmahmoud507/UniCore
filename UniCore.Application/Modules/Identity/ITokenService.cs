namespace UniCore.Application.Modules.Identity
{
    public interface ITokenService
    {
        AccessTokenResult CreateAccessToken(TokenSubject subject);
        GeneratedRefreshToken GenerateRefreshToken();
        string HashRefreshToken(string rawToken);
    }
}
