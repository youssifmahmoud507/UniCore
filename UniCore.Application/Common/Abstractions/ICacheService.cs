using UniCore.Domain.Common.Results;

namespace UniCore.Application.Common.Abstractions
{
    // Cache-aside for data that can always be rebuilt from the database (platform-services.md section 7).
    // The cache is NEVER the source of truth: when it fails, the factory (the database) answers and nothing breaks.
    public interface ICacheService
    {
        // A cached value, or the result of the factory (stored for ttl when it succeeds). A failed factory result is
        // returned as it is and never cached. Cache failures are invisible to the caller.
        Task<Result<T>> GetOrCreateAsync<T>(string group, string key, Func<CancellationToken, Task<Result<T>>> factory, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class;

        // Forget one entry.
        Task<Result> RemoveAsync(string group, string key, CancellationToken cancellationToken = default);

        // Forget every entry of a group at once (any instance of the API, the group version lives in the cache).
        Task<Result> InvalidateGroupAsync(string group, CancellationToken cancellationToken = default);
    }
}


