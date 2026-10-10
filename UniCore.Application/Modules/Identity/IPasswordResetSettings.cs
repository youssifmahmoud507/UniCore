namespace UniCore.Application.Modules.Identity
{
    public interface IPasswordResetSettings
    {
        string AppName { get; }
        int OtpLifetimeMinutes { get; }
        int MaxAttempts { get; }
        int ResendCooldownSeconds { get; }
        int MaxRequestsPerHour { get; }
        int ResetTokenMinutes { get; }
    }


}
