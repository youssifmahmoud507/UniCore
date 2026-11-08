using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data.Common;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Idempotency;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Idempotency;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Idempotency
{
    public sealed class IdempotencyService(AppDbContext db, IClock clock, IOptions<IdempotencyOptions> options, ILogger<IdempotencyService> logger) : IIdempotencyService
    {
        private readonly AppDbContext _db = db;
        private readonly IClock _clock = clock;
        private readonly IdempotencyOptions _options = options.Value;
        private readonly ILogger<IdempotencyService> _logger = logger;

        public async Task<Result<IdempotencyBegin>> BeginAsync(string? key, Guid userId, string requestHash, CancellationToken cancellationToken = default)
        {
            var now = _clock.UtcNow;
            var created = IdempotencyRecord.Begin(key, userId, requestHash, now, TimeSpan.FromHours(_options.ExpiryHours));

            if (!created.TryGetValue(out var record))
            {
                return Result<IdempotencyBegin>.Fail(created.Errors);
            }

            var staleAfter = TimeSpan.FromSeconds(_options.InProgressTimeoutSeconds);

            try
            {
                // Two tries: the second one runs after an expired or crashed record of the same key was removed.
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    _db.Set<IdempotencyRecord>().Add(record);

                    try
                    {
                        // The unique index (UserId, Key) lets exactly one of two parallel requests insert.
                        await _db.SaveChangesAsync(cancellationToken);

                        return IdempotencyBegin.Started(record.Id);
                    }
                    catch (DbUpdateException)
                    {
                        // Must not stay tracked: the business handler of this request saves on the same context.
                        _db.Entry(record).State = EntityState.Detached;
                    }

                    var existing = await _db.Set<IdempotencyRecord>().AsNoTracking()
                        .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == record.Key, cancellationToken);

                    if (existing is null)
                    {
                        // Not a duplicate key (or the row was just removed): try once more.
                        continue;
                    }

                    if (existing.IsExpired(now) || existing.IsStale(now, staleAfter))
                    {
                        await _db.Set<IdempotencyRecord>().Where(r => r.Id == existing.Id).ExecuteDeleteAsync(cancellationToken);
                        continue;
                    }

                    if (!existing.MatchesRequest(requestHash))
                    {
                        return IdempotencyBegin.Mismatch(existing.Id);
                    }

                    if (existing.Status == IdempotencyRecordStatus.Completed && existing.ResponseStatus is { } status && existing.ResponseBody is { } body)
                    {
                        return IdempotencyBegin.Replay(existing.Id, new StoredResponse(status, existing.ResponseContentType, existing.ResponseLocation, body));
                    }

                    return IdempotencyBegin.InProgress(existing.Id);
                }

                return IdempotencyErrors.StorageUnavailable;
            }
            catch (OperationCanceledException)
            {
                return CommonErrors.OperationCancelled;
            }
            catch (Exception ex) when (ex is DbException or DbUpdateException or TimeoutException or InvalidOperationException)
            {
                _logger.LogError("Claiming an idempotency key failed ({ExceptionType}).", ex.GetType().Name);
                return IdempotencyErrors.StorageUnavailable;
            }
        }

        public async Task<Result> CompleteAsync(Guid recordId, StoredResponse response, CancellationToken cancellationToken = default)
        {
            try
            {
                var record = await _db.Set<IdempotencyRecord>().FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken);

                if (record is null)
                {
                    return Result.Fail(IdempotencyErrors.RecordNotFound);
                }

                var completed = record.Complete(response.StatusCode, response.ContentType, response.Location, response.Body, _clock.UtcNow);

                if (completed.IsFailure)
                {
                    return completed;
                }

                await _db.SaveChangesAsync(cancellationToken);

                return Result.Ok();
            }
            catch (OperationCanceledException)
            {
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex) when (ex is DbException or DbUpdateException or TimeoutException or InvalidOperationException)
            {
                _logger.LogError("Storing an idempotency response failed ({ExceptionType}).", ex.GetType().Name);
                return Result.Fail(IdempotencyErrors.StorageUnavailable);
            }
        }

        public async Task<Result> AbandonAsync(Guid recordId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Only a record that is still running: a completed one is a real result and stays until it expires.
                await _db.Set<IdempotencyRecord>()
                    .Where(r => r.Id == recordId && r.Status == IdempotencyRecordStatus.InProgress)
                    .ExecuteDeleteAsync(cancellationToken);

                return Result.Ok();
            }
            catch (OperationCanceledException)
            {
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException)
            {
                _logger.LogError("Freeing an idempotency key failed ({ExceptionType}).", ex.GetType().Name);
                return Result.Fail(IdempotencyErrors.StorageUnavailable);
            }
        }
    }
}
