using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Runtime.CompilerServices;
using UniCore.Application.Common.Abstractions;

namespace UniCore.Infrastructure.Modules.Caching
{
    // After a successful save, clears the cache groups the saved changes made stale (CacheInvalidationRules).
    // It sees every save of the AppDbContext: handlers, the Identity user and role managers, jobs.
    public sealed class CacheInvalidationInterceptor(ICacheService cache) : SaveChangesInterceptor
    {
        private readonly ICacheService _cache = cache;

        // The groups found before the save, per DbContext (one interceptor serves every context).
        private readonly ConditionalWeakTable<DbContext, HashSet<string>> _pending = new();

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Collect(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Collect(eventData.Context);
            return ValueTask.FromResult(result);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            // Synchronous saves are not used by the application; blocking here is only a safety net.
            InvalidateAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
            return result;
        }

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            await InvalidateAsync(eventData.Context, cancellationToken);
            return result;
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData) => Forget(eventData.Context);

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            Forget(eventData.Context);
            return Task.CompletedTask;
        }

        public override void SaveChangesCanceled(DbContextEventData eventData) => Forget(eventData.Context);

        public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
        {
            Forget(eventData.Context);
            return Task.CompletedTask;
        }

        private void Collect(DbContext? context)
        {
            if (context is null)
            {
                return;
            }

            var groups = context.ChangeTracker.Entries()
                .Select(CacheInvalidationRules.GroupFor)
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

            if (groups.Count > 0)
            {
                _pending.AddOrUpdate(context, groups);
            }
        }

        private void Forget(DbContext? context)
        {
            if (context is not null)
            {
                _pending.Remove(context);
            }
        }

        private async Task InvalidateAsync(DbContext? context, CancellationToken cancellationToken)
        {
            if (context is null || !_pending.TryGetValue(context, out var groups))
            {
                return;
            }

            _pending.Remove(context);

            foreach (var group in groups)
            {
                // A failure is logged by the cache service and bounded by the entry expiry; the save already succeeded.
                await _cache.InvalidateGroupAsync(group, cancellationToken);
            }
        }
    }



}
