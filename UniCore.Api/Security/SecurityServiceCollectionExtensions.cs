using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace UniCore.Api.Security
{
    public static class SecurityServiceCollectionExtensions
    {
        // Bodies above this are refused before they are read. The upload actions raise it for themselves.
        public const long DefaultMaxRequestBodyBytes = 1_048_576;

        // CORS, HSTS and the trusted proxies. Wrong values stop the app at startup (framework behaviour).
        public static IServiceCollection AddApiSecurity(this IServiceCollection services)
        {
            services.AddSingleton<IValidateOptions<ApiCorsOptions>, ApiCorsOptionsValidator>();
            services.AddOptions<ApiCorsOptions>()
                .BindConfiguration(ApiCorsOptions.SectionName)
                .ValidateOnStart();

            services.AddOptions<ProxyOptions>()
                .BindConfiguration(ProxyOptions.SectionName)
                .Validate(ProxyOptions.IsValid, "Proxy:TrustedNetworks must contain IP addresses or CIDR ranges such as 10.0.0.0/8.")
                .ValidateOnStart();

            // The policy always exists. With no origins configured it simply allows nobody (fail closed).
            services.AddOptions<CorsOptions>()
                .Configure<IOptions<ApiCorsOptions>>((cors, apiCors) =>
                {
                    var origins = (apiCors.Value.AllowedOrigins ?? [])
                        .Where(ApiCorsOptionsValidator.IsValidOrigin)
                        .ToArray();

                    cors.AddPolicy(ApiCorsOptions.PolicyName, policy => policy
                        .WithOrigins(origins)
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                        .WithHeaders("Authorization", "Content-Type", "Accept", "Idempotency-Key", "X-Correlation-Id", "X-Requested-With", "X-SignalR-User-Agent")
                        .WithExposedHeaders("Retry-After", "X-Correlation-Id", "Content-Disposition")
                        .SetPreflightMaxAge(TimeSpan.FromMinutes(apiCors.Value.PreflightMaxAgeMinutes)));
                });
            services.AddCors();

            services.AddHsts(options =>
            {
                options.MaxAge = TimeSpan.FromDays(365);
                options.IncludeSubDomains = true;
                options.Preload = false;
            });

            services.AddOptions<ForwardedHeadersOptions>()
                .Configure<IOptions<ProxyOptions>>((forwarded, proxy) =>
                {
                    forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    forwarded.ForwardLimit = 1;
                    forwarded.KnownIPNetworks.Clear();
                    forwarded.KnownProxies.Clear();

                    foreach (var entry in proxy.Value.TrustedNetworks ?? [])
                    {
                        if (!ProxyOptions.TryParse(entry, out var network, out var address))
                        {
                            continue;
                        }

                        if (address is not null)
                        {
                            forwarded.KnownProxies.Add(address);
                        }
                        else
                        {
                            forwarded.KnownIPNetworks.Add(network);
                        }
                    }
                });

            return services;
        }

        // Must be the FIRST middleware, so everything after it (HTTPS redirect, rate limits, logs) sees the real client.
        public static WebApplication UseTrustedProxies(this WebApplication app)
        {
            var proxy = app.Services.GetRequiredService<IOptions<ProxyOptions>>().Value;

            if ((proxy.TrustedNetworks ?? []).Length > 0)
            {
                app.UseForwardedHeaders();
            }

            return app;
        }

        // No "Server: Kestrel" header, and a small default request body. Uploads raise the limit on their own actions.
        public static IWebHostBuilder ConfigureApiKestrel(this IWebHostBuilder webHost) =>
            webHost.ConfigureKestrel(kestrel =>
            {
                kestrel.AddServerHeader = false;
                kestrel.Limits.MaxRequestBodySize = DefaultMaxRequestBodyBytes;
            });
    }



}
