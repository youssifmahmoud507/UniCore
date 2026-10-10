using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class TokenService : ITokenService
    {
        private const int RefreshTokenBytes = 64;

        private readonly JwtOptions _options;
        private readonly IClock _clock;

        public TokenService(IOptions<JwtOptions> options, IClock clock)
        {
            _options = options.Value;
            _clock = clock;
        }

        public AccessTokenResult CreateAccessToken(TokenSubject subject)
        {
            var now = _clock.UtcNow;
            var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

            var claims = new List<Claim>
        {
            new(AppClaimTypes.Sub, subject.UserId.ToString()),
            new(AppClaimTypes.Jti, Guid.NewGuid().ToString("N")),
            new(AppClaimTypes.Email, subject.Email),
            new(AppClaimTypes.Name, subject.UserName)
        };

            if (subject.PersonId is { } personId)
            {
                claims.Add(new Claim(AppClaimTypes.PersonId, personId.ToString()));
            }

            // Roles only. Permissions and scopes are resolved server side per request.
            claims.AddRange(subject.Roles.Select(role => new Claim(AppClaimTypes.Role, role)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = expiresAt.UtcDateTime,
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };

            var token = new JsonWebTokenHandler().CreateToken(descriptor);

            return new AccessTokenResult(token, expiresAt);
        }

        public GeneratedRefreshToken GenerateRefreshToken()
        {
            var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RefreshTokenBytes));

            return new GeneratedRefreshToken(
                raw,
                HashRefreshToken(raw),
                _clock.UtcNow.AddDays(_options.RefreshTokenDays));
        }

        // SHA-256 is enough here: the token is 64 random bytes, so there is nothing to brute force.
        public string HashRefreshToken(string rawToken)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }

}
