using System.Globalization;
using System.Threading.RateLimiting;

namespace UniCore.Api.Extensions
{
    public static class RateLimitingExtensions
    {
        public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Values from security.md. The per-account limit comes from the Identity lockout.
                options.AddPolicy(RateLimitPolicies.Login, context => PerIp(context, permitLimit: 5, TimeSpan.FromMinutes(1)));
                options.AddPolicy(RateLimitPolicies.Refresh, context => PerIp(context, permitLimit: 20, TimeSpan.FromMinutes(1)));

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

        private static RateLimitPartition<string> PerIp(HttpContext context, int permitLimit, TimeSpan window)
            => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
    }
}
