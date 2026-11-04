using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data.Common;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Notifications;
using UniCore.Domain.Modules.Outbox;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Maintenance
{
    // Removes old rows that nobody needs any more. Each kind is deleted in one statement on the server (nothing is
    // loaded into memory), and one kind failing does not stop the others.
    public sealed class DataCleanupService(AppDbContext db, IClock clock, IOptions<MaintenanceOptions> options, ILogger<DataCleanupService> logger)
    {
        private readonly AppDbContext _db = db;
        private readonly IClock _clock = clock;
        private readonly MaintenanceOptions _options = options.Value;
        private readonly ILogger<DataCleanupService> _logger = logger;

        public async Task<Result<CleanupResult>> CleanAsync(CancellationToken cancellationToken = default)
        {
            var now = _clock.UtcNow;
            var failed = 0;

            var otpCutoff = now.AddDays(-_options.OtpRetentionDays);
            var tokenCutoff = now.AddDays(-_options.RefreshTokenRetentionDays);
            var readCutoff = now.AddDays(-_options.ReadNotificationRetentionDays);
            var unreadCutoff = now.AddDays(-_options.UnreadNotificationRetentionDays);
            var processedCutoff = now.AddDays(-_options.ProcessedOutboxRetentionDays);
            var failedCutoff = now.AddDays(-_options.FailedOutboxRetentionDays);

            var otps = await DeleteAsync("otp", () => _db.Set<PasswordResetOtp>().Where(o => o.ExpiresAt < otpCutoff).ExecuteDeleteAsync(cancellationToken), () => failed++);

            var tokens = await DeleteAsync("refresh token", () => _db.Set<RefreshToken>().Where(t => t.ExpiresAt < tokenCutoff || (t.RevokedAt != null && t.RevokedAt < tokenCutoff)).ExecuteDeleteAsync(cancellationToken), () => failed++);

            // The deliveries of a deleted notification go with it (cascade).
            var notifications = await DeleteAsync("notification", () => _db.Set<Notification>().Where(n => (n.ReadAt != null && n.ReadAt < readCutoff) || n.CreatedAt < unreadCutoff).ExecuteDeleteAsync(cancellationToken), () => failed++);

            // Pending and in-progress messages are NEVER deleted here.
            var outbox = await DeleteAsync("outbox", () => _db.Set<OutboxMessage>().Where(m => (m.Status == OutboxMessageStatus.Processed && m.ProcessedAt != null && m.ProcessedAt < processedCutoff) || (m.Status == OutboxMessageStatus.Failed && m.CreatedAt < failedCutoff)).ExecuteDeleteAsync(cancellationToken), () => failed++);

            if (cancellationToken.IsCancellationRequested)
            {
                return Result<CleanupResult>.Fail(CommonErrors.OperationCancelled);
            }

            _logger.LogInformation("Cleanup: {Otps} otps, {Tokens} refresh tokens, {Notifications} notifications, {Outbox} outbox messages deleted; {Failed} steps failed.", otps, tokens, notifications, outbox, failed);

            return new CleanupResult(otps, tokens, notifications, outbox, failed);
        }

        private async Task<int> DeleteAsync(string what, Func<Task<int>> delete, Action onFailure)
        {
            try
            {
                return await delete();
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException)
            {
                _logger.LogError("Cleanup of {What} failed ({ExceptionType}).", what, ex.GetType().Name);
                onFailure();
                return 0;
            }
        }
    }

}
