using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Linq.Expressions;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Outbox;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Outbox
{
    // Takes due messages from the outbox and runs their handlers. A message is CLAIMED with one atomic UPDATE
    // before its handler runs, so only one processor gets it. Every message runs in its own DI scope.
    public sealed class OutboxProcessor(IServiceScopeFactory scopes, IEnumerable<IOutboxMessageHandler> handlers, IClock clock, ILogger<OutboxProcessor> logger)
    {
        public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopes = scopes;
        private readonly Dictionary<string, IOutboxMessageHandler> _handlers = Index(handlers);
        private readonly IClock _clock = clock;
        private readonly ILogger<OutboxProcessor> _logger = logger;

        // Returns how many messages were processed in this batch.
        public async Task<Result<int>> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            var size = Math.Clamp(batchSize, 1, 200);
            List<Guid> ids;

            try
            {
                var now = _clock.UtcNow;

                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                ids = await db.Set<OutboxMessage>().AsNoTracking().Where(Due(now)).OrderBy(m => m.CreatedAt).Select(m => m.Id).Take(size).ToListAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Result<int>.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException)
            {
                _logger.LogError("Outbox could not be read ({ExceptionType}).", ex.GetType().Name);
                return Result<int>.Fail(OutboxErrors.StorageUnavailable);
            }

            var processed = 0;

            foreach (var id in ids)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var outcome = await ProcessOneAsync(id, cancellationToken);

                if (outcome.TryGetValue(out var wasProcessed))
                {
                    if (wasProcessed)
                    {
                        processed++;
                    }

                    continue;
                }

                return Result<int>.Fail(outcome.Errors);
            }

            return processed;
        }

        // Processes one specific message if it is due. Ok(true): processed here.
        // Ok(false): not due, not ours (another processor claimed it) or gone.
        public async Task<Result<bool>> ProcessOneAsync(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var services = scope.ServiceProvider;
                var db = services.GetRequiredService<AppDbContext>();
                var unitOfWork = services.GetRequiredService<IUnitOfWork>();

                var claimTime = _clock.UtcNow;
                DateTimeOffset? lockedUntil = claimTime.Add(LockDuration);

                var claimed = await db.Set<OutboxMessage>().Where(m => m.Id == id).Where(Due(claimTime)).ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, OutboxMessageStatus.Processing).SetProperty(m => m.LockedUntil, lockedUntil), cancellationToken);

                if (claimed == 0)
                {
                    return false;
                }

                var message = await db.Set<OutboxMessage>().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

                if (message is null)
                {
                    return false;
                }

                var redact = false;
                Result result;

                if (_handlers.TryGetValue(message.Type, out var handler))
                {
                    redact = handler.RedactPayloadWhenDone;
                    result = await RunHandlerAsync(handler, message.Payload, services, cancellationToken);
                }
                else
                {
                    result = Result.Fail(OutboxErrors.HandlerNotFound);
                }

                // Shutting down in the middle of a handler is not a failure of the message.
                if (cancellationToken.IsCancellationRequested && result.Error?.Code == CommonErrors.OperationCancelled.Code)
                {
                    return Result<bool>.Fail(CommonErrors.OperationCancelled);
                }

                var finishedAt = _clock.UtcNow;

                var finished = result.IsSuccess ? message.MarkProcessed(finishedAt, redact) : message.RecordFailure(result.Error?.Code, IsPermanent(result.Error), finishedAt, redact);

                if (finished.IsFailure)
                {
                    _logger.LogError("Outbox message {MessageId} could not be finished ({ErrorCode}).", id, finished.Error?.Code);
                    return Result<bool>.Fail(finished.Errors);
                }

                var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

                if (saved.IsFailure)
                {
                    _logger.LogError("Outbox message {MessageId} was handled but its state was not saved ({ErrorCode}).", id, saved.Error?.Code);
                    return Result<bool>.Fail(saved.Errors);
                }

                if (result.IsFailure)
                {
                    _logger.LogWarning("Outbox message {MessageId} ({MessageType}) failed with {ErrorCode}; status {Status}, attempts {Attempts}.", id, message.Type, result.Error?.Code, message.Status, message.RetryCount);
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                return Result<bool>.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException)
            {
                _logger.LogError("Outbox message {MessageId} could not be processed ({ExceptionType}).", id, ex.GetType().Name);
                return Result<bool>.Fail(OutboxErrors.StorageUnavailable);
            }
        }

        private async Task<Result> RunHandlerAsync(IOutboxMessageHandler handler, string payload, IServiceProvider services, CancellationToken cancellationToken)
        {
            try
            {
                return await handler.HandleAsync(payload, services, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex)
            {
                // Last line of defence around third-party code. Type only (messages can contain personal data).
                _logger.LogError("Outbox handler for {MessageType} failed unexpectedly ({ExceptionType}).", handler.MessageType, ex.GetType().Name);
                return Result.Fail(OutboxErrors.HandlerFailed);
            }
        }

        // Errors that retrying cannot fix go straight to the poison state.
        private static bool IsPermanent(Error? error) => error?.ErrorType is ErrorType.Validation or ErrorType.NotFound or ErrorType.Conflict or ErrorType.BusinessRule or ErrorType.Forbidden or ErrorType.Unauthorized or ErrorType.InvalidCredentials;

        private static Expression<Func<OutboxMessage, bool>> Due(DateTimeOffset now) => m => (m.Status == OutboxMessageStatus.Pending && (m.NextAttemptAt == null || m.NextAttemptAt <= now)) || (m.Status == OutboxMessageStatus.Processing && m.LockedUntil != null && m.LockedUntil <= now);

        private static Dictionary<string, IOutboxMessageHandler> Index(IEnumerable<IOutboxMessageHandler> handlers)
        {
            var index = new Dictionary<string, IOutboxMessageHandler>(StringComparer.Ordinal);

            foreach (var handler in handlers)
            {
                index.TryAdd(handler.MessageType, handler);
            }

            return index;
        }
    }



}
