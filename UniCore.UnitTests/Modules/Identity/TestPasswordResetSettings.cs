using UniCore.Application.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class TestPasswordResetSettings : IPasswordResetSettings
    {
        public string AppName { get; set; } = "UniCore";
        public int OtpLifetimeMinutes { get; set; } = 10;
        public int MaxAttempts { get; set; } = 5;
        public int ResendCooldownSeconds { get; set; } = 60;
        public int MaxRequestsPerHour { get; set; } = 5;
        public int ResetTokenMinutes { get; set; } = 10;
    }



}
