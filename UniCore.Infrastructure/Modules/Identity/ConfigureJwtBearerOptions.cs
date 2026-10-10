using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using UniCore.Application.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
    {
        private readonly JwtOptions _jwt;

        public ConfigureJwtBearerOptions(IOptions<JwtOptions> jwt)
        {
            _jwt = jwt.Value;
        }

        public void Configure(string? name, JwtBearerOptions options)
        {
            if (name == JwtBearerDefaults.AuthenticationScheme)
            {
                Configure(options);
            }
        }

        public void Configure(JwtBearerOptions options)
        {
            // Claim names stay as written in the token ("sub", "role"), no legacy remapping.
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)),
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(_jwt.ClockSkewSeconds),
                NameClaimType = AppClaimTypes.Name,
                RoleClaimType = AppClaimTypes.Role
            };
        }
    }

}
