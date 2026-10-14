using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Modules.Outbox;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Outbox
{
    // IEmailQueue on top of the outbox. It writes with its own DbContext (like the audit writer),
    // so it never saves or loses the caller's pending changes.
    public sealed class OutboxEmailQueue(IServiceScopeFactory scopes, IClock clock, IOutboxSignal signal, ILogger<OutboxEmailQueue> logger) : IEmailQueue
    {
        private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(5);

        private readonly IServiceScopeFactory _scopes = scopes;
        private readonly IClock _clock = clock;
        private readonly IOutboxSignal _signal = signal;
        private readonly ILogger<OutboxEmailQueue> _logger = logger;

        // The request token is not used on purpose: the business operation already happened,
        // so the email must be stored even if the client disconnected.
        public async Task<bool> TryEnqueueAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            var now = _clock.UtcNow;
            var created = OutboxMessageSerializer.FromObject(EmailOutboxHandler.EmailMessageType, message, now, now);

            if (!created.TryGetValue(out var outboxMessage))
            {
                _logger.LogError("An email could not be turned into an outbox message ({ErrorCode}).", created.Error?.Code);
                return false;
            }

            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetService<AppDbContext>();

                if (db is null)
                {
                    _logger.LogError("An email was not queued: the database context is not available.");
                    return false;
                }

                using var timeout = new CancellationTokenSource(WriteTimeout);

                db.Set<OutboxMessage>().Add(outboxMessage);
                await db.SaveChangesAsync(timeout.Token);
            }
            catch (Exception ex) when (ex is DbUpdateException or SqlException or InvalidOperationException or TimeoutException or OperationCanceledException)
            {
                // Type only: the message and the SQL text can contain the recipient or the OTP.
                _logger.LogError("An email was not queued ({ExceptionType}).", ex.GetType().Name);
                return false;
            }

            _signal.Signal();
            return true;
        }
    }



}
