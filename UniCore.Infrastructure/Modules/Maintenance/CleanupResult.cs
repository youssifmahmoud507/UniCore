namespace UniCore.Infrastructure.Modules.Maintenance
{
    public sealed record CleanupResult(int Otps, int RefreshTokens, int Notifications, int OutboxMessages, int IdempotencyRecords, int Failed);

}
