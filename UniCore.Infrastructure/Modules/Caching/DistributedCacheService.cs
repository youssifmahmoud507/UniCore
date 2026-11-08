using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;

namespace UniCore.Infrastructure.Modules.Caching
{
    // ICacheService on top of IDistributedCache (Redis in production, memory when Redis is not configured).
    //
    // Keys look like "{group}:{groupVersion}:{key}". Invalidating a group just replaces the group version, so every
    // old entry becomes unreachable at once, on every API instance, and expires by itself.
    //
    // Redis failures never reach the caller. After one, the cache is skipped for a short while (a "circuit breaker")
    // so a dead Redis does not add a timeout to every request.
    public sealed class DistributedCacheService(IDistributedCache cache, ILogger<DistributedCacheService> logger) : ICacheService
    {
        private const long BreakerMilliseconds = 30_000;
        private const string VersionPrefix = "version:";

        private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };
        private static readonly DistributedCacheEntryOptions NeverExpires = new();

        private readonly IDistributedCache _cache = cache;
        private readonly ILogger<DistributedCacheService> _logger = logger;
        private long _openUntil;

        private bool IsOpen => Environment.TickCount64 < Interlocked.Read(ref _openUntil);

        public async Task<Result<T>> GetOrCreateAsync<T>(string group, string key, Func<CancellationToken, Task<Result<T>>> factory, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
        {
            string? fullKey = null;

            if (!IsOpen)
            {
                try
                {
                    // The version is read BEFORE the factory runs: an invalidation during the factory then makes the
                    // value we store unreachable, never stale.
                    var version = await GetVersionAsync(group, cancellationToken);
                    fullKey = $"{group}:{version}:{key}";

                    var bytes = await _cache.GetAsync(fullKey, cancellationToken);

                    if (bytes is not null && TryRead<T>(bytes) is { } hit)
                    {
                        return Result<T>.Ok(hit);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return CommonErrors.OperationCancelled;
                }
                catch (Exception ex)
                {
                    Trip(ex);
                    fullKey = null;
                }
            }

            var created = await factory(cancellationToken);

            if (fullKey is not null && created.TryGetValue(out var value))
            {
                try
                {
                    var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
                    await _cache.SetAsync(fullKey, JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions), options, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // The value is fine, it just was not cached.
                }
                catch (Exception ex)
                {
                    Trip(ex);
                }
            }

            return created;
        }

        public async Task<Result> RemoveAsync(string group, string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var version = await GetVersionAsync(group, cancellationToken);
                await _cache.RemoveAsync($"{group}:{version}:{key}", cancellationToken);

                return Result.Ok();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex)
            {
                Trip(ex);
                return Result.Fail(CommonErrors.ExternalServiceUnavailable);
            }
        }

        // Always tried, even while the breaker is open: forgetting data matters more than speed.
        public async Task<Result> InvalidateGroupAsync(string group, CancellationToken cancellationToken = default)
        {
            try
            {
                await _cache.SetAsync(VersionPrefix + group, Encoding.UTF8.GetBytes(NewVersion()), NeverExpires, cancellationToken);

                return Result.Ok();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex)
            {
                Trip(ex);
                _logger.LogError("Cache group {Group} could not be invalidated; entries may stay until they expire.", group);
                return Result.Fail(CommonErrors.ExternalServiceUnavailable);
            }
        }

        private async Task<string> GetVersionAsync(string group, CancellationToken cancellationToken)
        {
            var versionKey = VersionPrefix + group;
            var bytes = await _cache.GetAsync(versionKey, cancellationToken);

            if (bytes is not null)
            {
                return Encoding.UTF8.GetString(bytes);
            }

            // First use, or Redis evicted the version. A NEW version (never "0" again) so entries written under an
            // earlier version can not come back to life.
            var fresh = NewVersion();
            await _cache.SetAsync(versionKey, Encoding.UTF8.GetBytes(fresh), NeverExpires, cancellationToken);

            return fresh;
        }

        private static string NewVersion() => Guid.NewGuid().ToString("N");

        // An entry that cannot be read (written by an older version of the API) counts as a miss.
        private static T? TryRead<T>(byte[] bytes) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(bytes, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        private void Trip(Exception exception)
        {
            var wasOpen = IsOpen;
            Interlocked.Exchange(ref _openUntil, Environment.TickCount64 + BreakerMilliseconds);

            if (!wasOpen)
            {
                _logger.LogWarning("The cache is unavailable ({ExceptionType}); using the database for the next {Seconds} seconds.", exception.GetType().Name, BreakerMilliseconds / 1000);
            }
        }
    }



}
