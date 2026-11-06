using Microsoft.Extensions.Options;
using System.Globalization;
using System.Threading.RateLimiting;
using UniCore.Application.Modules.Identity;

namespace UniCore.Api.Extensions
{
    public static class RateLimitingExtensions
    {
        public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
        {
            services.AddOptions<RateLimitingOptions>()
                .BindConfiguration(RateLimitingOptions.SectionName)
                .Validate(RateLimitingOptions.IsValid, "RateLimiting has an invalid value (every limit must be 1 to 100000).")
                .ValidateOnStart();

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Every request passes this one too: 100 per minute per signed-in user (per IP when anonymous). Health checks are exempt.
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    context.Request.Path.StartsWithSegments("/health")
                        ? RateLimitPartition.GetNoLimiter("health")
                        : Limited(context, Caller(context), Settings(context).GeneralPerMinute, TimeSpan.FromMinutes(1)));

                // Values from security.md. The per-account limit comes from the Identity lockout.
                options.AddPolicy(RateLimitPolicies.Login, context => Limited(context, Ip(context), 5, TimeSpan.FromMinutes(1)));
                options.AddPolicy(RateLimitPolicies.Refresh, context => Limited(context, Ip(context), 20, TimeSpan.FromMinutes(1)));
                options.AddPolicy(RateLimitPolicies.ForgotPassword, context => Limited(context, Ip(context), 10, TimeSpan.FromHours(1)));
                options.AddPolicy(RateLimitPolicies.VerifyOtp, context => Limited(context, Ip(context), 5, TimeSpan.FromMinutes(10)));
                options.AddPolicy(RateLimitPolicies.ResetPassword, context => Limited(context, Ip(context), 5, TimeSpan.FromMinutes(10)));
                options.AddPolicy(RateLimitPolicies.Upload, context => Limited(context, Caller(context), Settings(context).UploadPerMinute, TimeSpan.FromMinutes(1)));

                options.OnRejected = async (context, cancellationToken) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                    }

                    var problem = Results.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Too Many Requests",
                        detail: "Too many requests. Try again later.",
                        type: "urn:unicore:error:RATE_LIMIT_EXCEEDED",
                        instance: context.HttpContext.Request.Path,
                        extensions: new Dictionary<string, object?>
                        {
                            ["errorCode"] = "RATE_LIMIT_EXCEEDED",
                            ["traceId"] = context.HttpContext.TraceIdentifier
                        });

                    await problem.ExecuteAsync(context.HttpContext);
                };
            });

            return services;
        }

        private static RateLimitingOptions Settings(HttpContext context) =>
            context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        private static string Ip(HttpContext context) => "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        // The signed-in user, or the IP address when nobody is signed in. The limiter runs after authentication, so the user is known.
        private static string Caller(HttpContext context)
        {
            var subject = context.User.Identity?.IsAuthenticated == true ? context.User.FindFirst(AppClaimTypes.Sub)?.Value : null;

            return string.IsNullOrEmpty(subject) ? Ip(context) : "user:" + subject;
        }

        private static RateLimitPartition<string> Limited(HttpContext context, string key, int permitLimit, TimeSpan window)
        {
            // Integration tests switch the limiter off with RateLimiting:Disabled=true.
            // It is honoured only in the Development environment, so production can never lose it.
            var environment = context.RequestServices.GetService<IWebHostEnvironment>();
            var configuration = context.RequestServices.GetService<IConfiguration>();

            if (environment?.IsDevelopment() == true && configuration?.GetValue<bool>("RateLimiting:Disabled") == true)
            {
                return RateLimitPartition.GetNoLimiter("disabled");
            }

            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            });
        }
    }




}
