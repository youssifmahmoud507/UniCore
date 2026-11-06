namespace UniCore.Api.Extensions
{
    // appsettings: "RateLimiting": { "GeneralPerMinute": 100, "UploadPerMinute": 20 } (both optional)
    // The auth limits are fixed in code (security.md section 3).
    public sealed class RateLimitingOptions
    {
        public const string SectionName = "RateLimiting";

        // Every endpoint: per signed-in user, or per IP when nobody is signed in.
        public int GeneralPerMinute { get; set; } = 100;

        // Uploads are the most expensive calls: per signed-in user.
        public int UploadPerMinute { get; set; } = 20;

        public static bool IsValid(RateLimitingOptions options) => options.GeneralPerMinute is >= 1 and <= 100_000 && options.UploadPerMinute is >= 1 and <= 100_000;
    }




}
