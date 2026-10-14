using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Outbox;
using UniCore.Infrastructure.Modules.Outbox;

namespace UniCore.Infrastructure.Persistence
{
    public sealed class UnitOfWork(AppDbContext db, IClock clock, ILogger<UnitOfWork> logger) : IUnitOfWork
    {
        private const int UniqueIndexViolation = 2601;
        private const int UniqueConstraintViolation = 2627;
        private const int SqlTimeout = -2;
        private readonly AppDbContext _db = db;
        private readonly IClock _clock = clock;
        private readonly ILogger<UnitOfWork> _logger = logger;

        private sealed record StagedEvents(IReadOnlyList<AggregateRoot> Aggregates, IReadOnlyList<OutboxMessage> Messages);

        // Domain events raised by the tracked aggregates become outbox rows saved in the SAME transaction.
        // The aggregates' events are cleared only after a successful save, so a failed save can be retried.
        public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var staged = StageDomainEvents();

            if (!staged.TryGetValue(out var events))
            {
                return Result<int>.Fail(staged.Errors);
            }

            var saved = await SaveCoreAsync(cancellationToken);

            if (saved.IsSuccess)
            {
                foreach (var aggregate in events.Aggregates)
                {
                    aggregate.ClearDomainEvents();
                }
            }
            else
            {
                // Without this, a retry would add the same outbox rows a second time.
                foreach (var message in events.Messages)
                {
                    _db.Entry(message).State = EntityState.Detached;
                }
            }

            return saved;
        }

        private Result<StagedEvents> StageDomainEvents()
        {
            var aggregates = _db.ChangeTracker.Entries<AggregateRoot>().Select(entry => entry.Entity).Where(aggregate => aggregate.DomainEvents.Count > 0).ToList();

            var messages = new List<OutboxMessage>();
            var now = _clock.UtcNow;

            foreach (var domainEvent in aggregates.SelectMany(aggregate => aggregate.DomainEvents))
            {
                var message = OutboxMessageSerializer.FromDomainEvent(domainEvent, now);

                if (!message.TryGetValue(out var outboxMessage))
                {
                    _logger.LogError("A domain event of type {EventType} could not be stored in the outbox ({ErrorCode}).", domainEvent.GetType().Name, message.Error?.Code);
                    return Result<StagedEvents>.Fail(message.Errors);
                }

                messages.Add(outboxMessage);
            }

            _db.Set<OutboxMessage>().AddRange(messages);

            return new StagedEvents(aggregates, messages);
        }

        private async Task<Result<int>> SaveCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                var rows = await _db.SaveChangesAsync(cancellationToken);
                return Result<int>.Ok(rows);
            }
            // DbUpdateConcurrencyException derives from DbUpdateException, so it must come first.
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict while saving changes.");
                return Result<int>.Fail(CommonErrors.ConcurrencyConflict);
            }
            catch (DbUpdateException ex) when (SqlErrorNumber(ex) is UniqueIndexViolation or UniqueConstraintViolation)
            {
                // Not logging the exception: SQL messages for unique violations contain the duplicate value.
                _logger.LogWarning("Unique constraint violation while saving changes (SQL error {SqlError}).", SqlErrorNumber(ex));
                return Result<int>.Fail(CommonErrors.DuplicateResource);
            }
            catch (DbUpdateException ex) when (SqlErrorNumber(ex) == SqlTimeout)
            {
                _logger.LogError("Database timeout while saving changes.");
                return Result<int>.Fail(CommonErrors.DatabaseTimeout);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError("Database update failed ({ExceptionType}, SQL error {SqlError}).",
                    ex.GetType().Name, SqlErrorNumber(ex));
                return Result<int>.Fail(CommonErrors.DatabaseUpdateFailed);
            }
            catch (TimeoutException)
            {
                _logger.LogError("Timeout while saving changes.");
                return Result<int>.Fail(CommonErrors.DatabaseTimeout);
            }
            catch (OperationCanceledException)
            {
                return Result<int>.Fail(CommonErrors.OperationCancelled);
            }
        }

        private static int? SqlErrorNumber(DbUpdateException ex) => (ex.InnerException as SqlException)?.Number;
    }
}
