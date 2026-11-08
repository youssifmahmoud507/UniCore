using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Caching
{
    public static class CachingServiceCollectionExtensions
    {
        // Must run AFTER the identity registrations: it puts the cached provider in front of UserAccessProvider.
        public static IServiceCollection AddCachingModule(this IServiceCollection services)
        {
            services.AddOptions<RedisOptions>()
                .BindConfiguration(RedisOptions.SectionName)
                .Validate(RedisOptions.IsValid, "Redis has an invalid value (connection string, instance name or a timeout of 100 to 30000 ms).")
                .ValidateOnStart();

            services.AddSingleton<IDistributedCache>(CreateCache);
            services.AddSingleton<ICacheService, DistributedCacheService>();
            services.AddSingleton<CacheInvalidationInterceptor>();

            services.AddScoped<UserAccessProvider>();
            services.Replace(ServiceDescriptor.Scoped<IUserAccessProvider>(provider => new CachedUserAccessProvider(
                provider.GetRequiredService<UserAccessProvider>(),
                provider.GetRequiredService<ICacheService>())));

            services.AddHealthChecks().AddCheck<CacheHealthCheck>("cache", failureStatus: HealthStatus.Degraded, tags: ["ready"]);

            return services;
        }

        private static IDistributedCache CreateCache(IServiceProvider provider)
        {
            var options = provider.GetRequiredService<IOptions<RedisOptions>>().Value;

            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                return new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            }

            var redis = ConfigurationOptions.Parse(options.ConnectionString);
            redis.AbortOnConnectFail = false;
            redis.ConnectTimeout = options.ConnectTimeoutMs;
            redis.SyncTimeout = options.CommandTimeoutMs;
            redis.AsyncTimeout = options.CommandTimeoutMs;

            return new RedisCache(Options.Create(new RedisCacheOptions
            {
                ConfigurationOptions = redis,
                InstanceName = options.InstanceName
            }));
        }
    }



}
