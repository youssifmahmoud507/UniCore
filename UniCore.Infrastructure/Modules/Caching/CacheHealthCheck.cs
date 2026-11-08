using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace UniCore.Infrastructure.Modules.Caching
{
    // Part of /health/ready. A broken cache is "Degraded", not "Unhealthy": the API still works from the database.
    public sealed class CacheHealthCheck(IDistributedCache cache) : IHealthCheck
    {
        private const string ProbeKey = "health:probe";

        private readonly IDistributedCache _cache = cache;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));

                var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) };
                await _cache.SetAsync(ProbeKey, [1], options, timeout.Token);
                var value = await _cache.GetAsync(ProbeKey, timeout.Token);

                return value is { Length: 1 } ? HealthCheckResult.Healthy("The cache answers.") : HealthCheckResult.Degraded("The cache did not return the probe value.");
            }
            catch (Exception)
            {
                return HealthCheckResult.Degraded("The cache is unavailable; the API falls back to the database.");
            }
        }
    }



}
