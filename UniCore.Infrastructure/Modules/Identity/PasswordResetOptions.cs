using UniCore.Application.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class PasswordResetOptions : IPasswordResetSettings
    {
        public const string SectionName = "PasswordReset";

        // Secret (user-secrets / environment variable). Keys the OTP hash so a leaked table is useless.
        public string Pepper { get; set; } = string.Empty;

        public string AppName { get; set; } = "UniCore";
        public int OtpLifetimeMinutes { get; set; } = 10;
        public int MaxAttempts { get; set; } = 5;
        public int ResendCooldownSeconds { get; set; } = 60;
        public int MaxRequestsPerHour { get; set; } = 5;
        public int ResetTokenMinutes { get; set; } = 10;
    }




}
