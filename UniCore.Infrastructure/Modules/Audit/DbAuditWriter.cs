using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Modules.Audit;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Audit
{
    // Writes audit rows with its own DbContext, so it never saves (or loses) the caller's pending changes,
    // and the row survives a cancelled request. A failed audit write is logged and never breaks the request.
    // Replaced by event-driven auditing through the Outbox later; IAuditWriter does not change.
    public sealed class DbAuditWriter(IServiceScopeFactory scopes, IHttpContextAccessor httpContextAccessor, IClock clock, ILogger<DbAuditWriter> logger) : IAuditWriter
    {
        private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(5);

        private readonly IServiceScopeFactory _scopes = scopes;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly IClock _clock = clock;
        private readonly ILogger<DbAuditWriter> _logger = logger;

        // The request token is not used on purpose: the business operation already happened,
        // so its audit row must be written even if the client disconnected.
        public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            var created = AuditLog.Create(entry.Action, entry.ActorUserId, entry.EntityName, entry.EntityId, entry.IpAddress, entry.UserAgent, entry.OldValues, entry.NewValues, _httpContextAccessor.HttpContext?.TraceIdentifier, _clock.UtcNow);

            if (!created.TryGetValue(out var log))
            {
                _logger.LogError("Audit entry rejected ({ErrorCodes}).", string.Join(",", created.Errors.Select(e => e.Code)));
                return;
            }

            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetService<AppDbContext>();

                if (db is null)
                {
                    _logger.LogError("Audit entry {Action} was not written: the database context is not available.", log.Action);
                    return;
                }

                using var timeout = new CancellationTokenSource(WriteTimeout);

                db.Set<AuditLog>().Add(log);
                await db.SaveChangesAsync(timeout.Token);
            }
            catch (Exception ex) when (ex is DbUpdateException or SqlException or InvalidOperationException or TimeoutException or OperationCanceledException)
            {
                // Type only: SQL messages can contain values.
                _logger.LogError("Audit entry {Action} was not written ({ExceptionType}).", log.Action, ex.GetType().Name);
            }
        }
    }


}
