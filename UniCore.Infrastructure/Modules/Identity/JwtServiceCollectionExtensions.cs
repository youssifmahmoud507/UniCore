using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniCore.Application.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public static class JwtServiceCollectionExtensions
    {
        public static IServiceCollection AddJwtAuthentication(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

            // ValidateOnStart: a missing or weak signing key stops the app at startup (framework behaviour).
            services.AddOptions<JwtOptions>()
                .Bind(configuration.GetSection(JwtOptions.SectionName))
                .ValidateOnStart();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();

            services.ConfigureOptions<ConfigureJwtBearerOptions>();
            services.AddSingleton<ITokenService, TokenService>();

            return services;
        }
    }

}
