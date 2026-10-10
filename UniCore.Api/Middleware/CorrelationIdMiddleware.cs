using Serilog.Context;

namespace UniCore.Api.Middleware
{
    public sealed class CorrelationIdMiddleware(RequestDelegate next)
    {
        public const string HeaderName = "X-Correlation-Id";

        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = ResolveCorrelationId(context);

            context.TraceIdentifier = correlationId;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[HeaderName] = correlationId;
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }

        private static string ResolveCorrelationId(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(HeaderName, out var values))
            {
                var candidate = values.ToString();
                if (IsValid(candidate))
                {
                    return candidate;
                }
            }

            return Guid.NewGuid().ToString("N");
        }

        // Accept only safe values so a client cannot inject odd content into the logs.
        private static bool IsValid(string value) => value.Length is > 0 and <= 64 && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
    }
}
