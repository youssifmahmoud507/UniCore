using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text;
using UniCore.Domain.Common.Results;
using UniCore.Infrastructure.Modules.Caching;

namespace UniCore.IntegrationTests.Caching
{
    // The cache layer with an in-memory cache underneath: no Redis needed.
    public class DistributedCacheServiceTests
    {
        private sealed record Box(string Name, int Number);

        private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

        private readonly MemoryDistributedCache _memory = new(Options.Create(new MemoryDistributedCacheOptions()));
        private int _factoryCalls;

        private DistributedCacheService Service(IDistributedCache? cache = null)
            => new(cache ?? _memory, NullLogger<DistributedCacheService>.Instance);

        private Task<Result<Box>> Factory(CancellationToken cancellationToken)
        {
            _factoryCalls++;
            return Task.FromResult(Result<Box>.Ok(new Box("value", _factoryCalls)));
        }

        [Fact]
        public async Task The_second_read_comes_from_the_cache()
        {
            var cache = Service();

            var first = await cache.GetOrCreateAsync("group", "key", Factory, Minute);
            var second = await cache.GetOrCreateAsync("group", "key", Factory, Minute);

            Assert.True(first.IsSuccess);
            Assert.Equal(1, _factoryCalls);
            Assert.Equal(1, second.Value!.Number);
        }

        [Fact]
        public async Task Different_keys_do_not_share_an_entry()
        {
            var cache = Service();

            var a = await cache.GetOrCreateAsync("group", "a", Factory, Minute);
            var b = await cache.GetOrCreateAsync("group", "b", Factory, Minute);

            Assert.Equal(2, _factoryCalls);
            Assert.NotEqual(a.Value!.Number, b.Value!.Number);
        }

        [Fact]
        public async Task Invalidating_a_group_clears_only_that_group()
        {
            var cache = Service();
            var kept = await cache.GetOrCreateAsync("other", "key", Factory, Minute);
            await cache.GetOrCreateAsync("group", "key", Factory, Minute);

            var invalidated = await cache.InvalidateGroupAsync("group");
            var rebuilt = await cache.GetOrCreateAsync("group", "key", Factory, Minute);
            var stillCached = await cache.GetOrCreateAsync("other", "key", Factory, Minute);

            Assert.True(invalidated.IsSuccess);
            Assert.Equal(3, rebuilt.Value!.Number);
            Assert.Equal(kept.Value!.Number, stillCached.Value!.Number);
        }

        [Fact]
        public async Task Removing_a_key_clears_only_that_entry()
        {
            var cache = Service();
            await cache.GetOrCreateAsync("group", "a", Factory, Minute);
            var b = await cache.GetOrCreateAsync("group", "b", Factory, Minute);

            Assert.True((await cache.RemoveAsync("group", "a")).IsSuccess);

            var rebuilt = await cache.GetOrCreateAsync("group", "a", Factory, Minute);
            var kept = await cache.GetOrCreateAsync("group", "b", Factory, Minute);

            Assert.Equal(3, rebuilt.Value!.Number);
            Assert.Equal(b.Value!.Number, kept.Value!.Number);
        }

        [Fact]
        public async Task A_failed_factory_is_returned_and_never_cached()
        {
            var cache = Service();
            var runs = 0;

            Task<Result<Box>> Failing(CancellationToken token)
            {
                runs++;
                return Task.FromResult(Result<Box>.Fail(Error.NotFound("THING_NOT_FOUND", "No such thing.")));
            }

            var first = await cache.GetOrCreateAsync("group", "missing", Failing, Minute);
            var second = await cache.GetOrCreateAsync("group", "missing", Failing, Minute);

            Assert.True(first.IsFailure);
            Assert.Equal("THING_NOT_FOUND", first.Errors[0].Code);
            Assert.True(second.IsFailure);
            Assert.Equal(2, runs);
        }

        [Fact]
        public async Task An_expired_entry_is_rebuilt()
        {
            var cache = Service();

            await cache.GetOrCreateAsync("group", "key", Factory, TimeSpan.FromMilliseconds(100));
            await Task.Delay(400);
            var rebuilt = await cache.GetOrCreateAsync("group", "key", Factory, Minute);

            Assert.Equal(2, rebuilt.Value!.Number);
        }

        [Fact]
        public async Task A_cache_that_is_down_never_breaks_a_read()
        {
            var flaky = new FlakyDistributedCache(_memory) { Broken = true };
            var cache = Service(flaky);

            var result = await cache.GetOrCreateAsync("group", "key", Factory, Minute);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value!.Number);
        }

        [Fact]
        public async Task After_a_failure_the_cache_is_skipped_for_a_while()
        {
            var flaky = new FlakyDistributedCache(_memory) { Broken = true };
            var cache = Service(flaky);
            await cache.GetOrCreateAsync("group", "key", Factory, Minute);
            var callsAfterFailure = flaky.Calls;

            var again = await cache.GetOrCreateAsync("group", "key", Factory, Minute);

            Assert.True(again.IsSuccess);
            Assert.Equal(callsAfterFailure, flaky.Calls);
        }

        [Fact]
        public async Task Invalidation_and_removal_report_a_down_cache_as_a_failed_result()
        {
            var flaky = new FlakyDistributedCache(_memory) { Broken = true };
            var cache = Service(flaky);

            var invalidated = await cache.InvalidateGroupAsync("group");
            var removed = await cache.RemoveAsync("group", "key");

            Assert.True(invalidated.IsFailure);
            Assert.Equal("EXTERNAL_SERVICE_UNAVAILABLE", invalidated.Errors[0].Code);
            Assert.True(removed.IsFailure);
        }

        [Fact]
        public async Task An_unreadable_entry_counts_as_a_miss_and_is_replaced()
        {
            var cache = Service();
            await cache.GetOrCreateAsync("group", "key", Factory, Minute);
            var version = Encoding.UTF8.GetString((await _memory.GetAsync("version:group"))!);
            await _memory.SetAsync($"group:{version}:key", [1, 2, 3], new DistributedCacheEntryOptions());

            var rebuilt = await cache.GetOrCreateAsync("group", "key", Factory, Minute);
            var cached = await cache.GetOrCreateAsync("group", "key", Factory, Minute);

            Assert.Equal(2, rebuilt.Value!.Number);
            Assert.Equal(2, cached.Value!.Number);
        }

        [Fact]
        public async Task A_lost_group_version_never_brings_old_entries_back()
        {
            var cache = Service();
            await cache.GetOrCreateAsync("group", "key", Factory, Minute);

            // What an evicting Redis may do to the version key.
            await _memory.RemoveAsync("version:group");
            var rebuilt = await cache.GetOrCreateAsync("group", "key", Factory, Minute);

            Assert.Equal(2, rebuilt.Value!.Number);
        }

        [Fact]
        public async Task A_cancelled_read_is_a_result_and_not_an_exception()
        {
            var cache = Service(new FlakyDistributedCache(_memory));
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();

            var result = await cache.GetOrCreateAsync("group", "key", Factory, Minute, cancelled.Token);

            Assert.True(result.IsFailure);
            Assert.Equal(0, _factoryCalls);
        }
    }
}
