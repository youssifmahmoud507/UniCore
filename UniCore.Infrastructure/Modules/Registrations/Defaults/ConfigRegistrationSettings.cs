using Microsoft.Extensions.Configuration;
using UniCore.Application.Modules.Registrations;

namespace UniCore.Infrastructure.Modules.Registrations.Defaults
{
    // Temporary home of the registration numbers until a settings module exists (open question for Developer 2).
    // appsettings:  "Registration": { "RegularCreditLimit": 18, "ProbationCreditLimit": 12, "SummerCreditLimit": 9,
    //               "SummerTermCode": "SUMMER", "AllowGradeImprovement": true, "ImprovementMaxGradePoint": 2.0 }
    // Anything missing or unreadable falls back to the default shown here.
    public sealed class ConfigRegistrationSettings(IConfiguration configuration) : IRegistrationSettings
    {
        private readonly IConfigurationSection _section = configuration.GetSection("Registration");

        public int RegularCreditLimit => Read("RegularCreditLimit", 18);
        public int ProbationCreditLimit => Read("ProbationCreditLimit", 12);
        public int SummerCreditLimit => Read("SummerCreditLimit", 9);
        public string SummerTermCode => string.IsNullOrWhiteSpace(_section["SummerTermCode"]) ? "SUMMER" : _section["SummerTermCode"]!.Trim();
        public bool AllowGradeImprovement => Read("AllowGradeImprovement", true);
        public decimal ImprovementMaxGradePoint => Read("ImprovementMaxGradePoint", 2.0m);

        private T Read<T>(string key, T fallback) => _section.GetValue(key, fallback)!;
    }
}
