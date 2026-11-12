using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;

namespace UniCore.UnitTests.Modules.Reporting
{
    internal sealed class FakeCacheService : ICacheService
    {
        public Dictionary<string, object> Entries { get; } = [];
        public int FactoryCalls { get; private set; }

        public async Task<Result<T>> GetOrCreateAsync<T>(string group, string key, Func<CancellationToken, Task<Result<T>>> factory, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
        {
            var full = $"{group}:{key}";

            if (Entries.TryGetValue(full, out var hit) && hit is T cached)
            {
                return Result<T>.Ok(cached);
            }

            FactoryCalls++;
            var created = await factory(cancellationToken);

            if (created.TryGetValue(out var value))
            {
                Entries[full] = value;
            }

            return created;
        }

        public Task<Result> RemoveAsync(string group, string key, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok());

        public Task<Result> InvalidateGroupAsync(string group, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok());
    }




}
