using Microsoft.Extensions.Options;

namespace UniCore.Api.Security
{
    public sealed class ApiCorsOptionsValidator : IValidateOptions<ApiCorsOptions>
    {
        public ValidateOptionsResult Validate(string? name, ApiCorsOptions options)
        {
            var failures = new List<string>();
            var origins = options.AllowedOrigins ?? [];

            for (var i = 0; i < origins.Length; i++)
            {
                if (!IsValidOrigin(origins[i]))
                {
                    failures.Add($"Cors:AllowedOrigins:{i} must be a plain origin such as https://app.example.com (no wildcard, path, query or trailing slash, and http only for localhost).");
                }
            }

            if (options.PreflightMaxAgeMinutes is < 0 or > 1440)
            {
                failures.Add("Cors:PreflightMaxAgeMinutes must be between 0 and 1440.");
            }

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }

        // The browser sends exactly scheme://host[:port], so anything else could never match (or would be too wide).
        public static bool IsValidOrigin(string? origin)
        {
            if (string.IsNullOrWhiteSpace(origin) || origin != origin.Trim() || origin.Contains('*') || origin.EndsWith('/'))
            {
                return false;
            }

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            {
                return false;
            }

            if (uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath != "/")
            {
                return false;
            }

            return uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback;
        }
    }



}
