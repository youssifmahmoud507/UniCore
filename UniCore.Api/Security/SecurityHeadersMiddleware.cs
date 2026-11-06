namespace UniCore.Api.Security
{
    // The HTTP security headers of security.md section 4, on every response (errors and 429 included).
    public sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        public const string ContentSecurityPolicyValue = "default-src 'none'; frame-ancestors 'none'";

        private static readonly PathString AuthPath = new("/api/v1/auth");
        private static readonly PathString[] DocumentationPaths = [new("/scalar"), new("/openapi")];

        private readonly RequestDelegate _next = next;
        private readonly bool _isDevelopment = environment.IsDevelopment();

        public Task InvokeAsync(HttpContext context)
        {
            // OnStarting: the headers are added right before the response goes out, whoever produced it.
            context.Response.OnStarting(() =>
            {
                Apply(context);
                return Task.CompletedTask;
            });

            return _next(context);
        }

        private void Apply(HttpContext context)
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";

            // The API never serves pages. The one exception is the Scalar/OpenAPI documentation, in Development only.
            if (!(_isDevelopment && IsDocumentation(context.Request.Path)))
            {
                headers["Content-Security-Policy"] = ContentSecurityPolicyValue;
            }

            // Tokens travel in auth responses: no cache or proxy may keep them.
            if (context.Request.Path.StartsWithSegments(AuthPath))
            {
                headers["Cache-Control"] = "no-store";
            }
        }

        private static bool IsDocumentation(PathString path) => DocumentationPaths.Any(path.StartsWithSegments);
    }



}
