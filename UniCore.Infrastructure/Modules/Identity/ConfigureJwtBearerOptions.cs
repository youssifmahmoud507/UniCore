using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using UniCore.Application.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwt) : IConfigureNamedOptions<JwtBearerOptions>
    {
        private readonly JwtOptions _jwt = jwt.Value;

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

            options.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    await Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Unauthorized",
                        detail: "Authentication is required or has failed.",
                        type: "urn:unicore:error:GENERAL_UNAUTHORIZED",
                        instance: context.Request.Path,
                        extensions: new Dictionary<string, object?>
                        {
                            ["errorCode"] = "GENERAL_UNAUTHORIZED",
                            ["traceId"] = context.HttpContext.TraceIdentifier
                        }).ExecuteAsync(context.HttpContext);
                },
                OnForbidden = async context =>
                {
                    await Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "Forbidden",
                        detail: "This operation is forbidden.",
                        type: "urn:unicore:error:GENERAL_FORBIDDEN",
                        instance: context.Request.Path,
                        extensions: new Dictionary<string, object?>
                        {
                            ["errorCode"] = "GENERAL_FORBIDDEN",
                            ["traceId"] = context.HttpContext.TraceIdentifier
                        }).ExecuteAsync(context.HttpContext);
                }
            };
        }
    }

}
