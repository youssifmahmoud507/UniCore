using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;

namespace UniCore.IntegrationTests.Caching
{
    // A cache that can be "switched off", like a Redis that went down. Counts how often it was touched, and honours
    // cancellation the way a real network cache does. Failures are faulted tasks: no throw anywhere (Rule A).
    public sealed class FlakyDistributedCache(IDistributedCache inner) : IDistributedCache
    {
        private readonly IDistributedCache _inner = inner;

        public bool Broken { get; set; }

        public int Calls { get; private set; }

        // Null when the call may go on; otherwise the task that fails the call.
        private Task? Fail(CancellationToken token = default)
        {
            Calls++;

            if (token.IsCancellationRequested)
            {
                return Task.FromCanceled(token);
            }

            return Broken ? Task.FromException(new TimeoutException("The cache is down.")) : null;
        }

        public byte[]? Get(string key)
        {
            Fail()?.GetAwaiter().GetResult();
            return _inner.Get(key);
        }

        public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            if (Fail(token) is { } failure)
            {
                await failure;
            }

            return await _inner.GetAsync(key, token);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            Fail()?.GetAwaiter().GetResult();
            _inner.Set(key, value, options);
        }

        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            if (Fail(token) is { } failure)
            {
                await failure;
            }

            await _inner.SetAsync(key, value, options, token);
        }

        public void Refresh(string key) => _inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => _inner.RefreshAsync(key, token);

        public void Remove(string key)
        {
            Fail()?.GetAwaiter().GetResult();
            _inner.Remove(key);
        }

        public async Task RemoveAsync(string key, CancellationToken token = default)
        {
            if (Fail(token) is { } failure)
            {
                await failure;
            }

            await _inner.RemoveAsync(key, token);
        }
    }
}
