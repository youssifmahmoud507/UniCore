namespace UniCore.Api.Security
{
    // appsettings: "Cors": { "AllowedOrigins": [ "https://app.example.com" ], "PreflightMaxAgeMinutes": 10 }
    // No origins (the default) means no browser from another origin may call the API. Never "*".
    public sealed class ApiCorsOptions
    {
        public const string SectionName = "Cors";
        public const string PolicyName = "UniCoreApi";

        public string[] AllowedOrigins { get; set; } = [];

        public int PreflightMaxAgeMinutes { get; set; } = 10;
    }



}
