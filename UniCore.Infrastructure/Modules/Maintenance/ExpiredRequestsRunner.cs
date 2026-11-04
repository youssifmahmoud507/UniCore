using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Common.Results;

namespace UniCore.Infrastructure.Modules.Maintenance
{
    // Every request is cancelled in its own DI scope (its own DbContext), so a failed save for one request
    // (a concurrency conflict, for example) leaves nothing behind that could spoil the next one.
    public sealed class ExpiredRequestsRunner(IServiceScopeFactory scopes, IOptions<MaintenanceOptions> options)
    {
        private readonly IServiceScopeFactory _scopes = scopes;
        private readonly int _inactiveDays = options.Value.ExpiredRequestDays;

        public async Task<Result<ExpiredRequestsResult>> RunAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Guid> ids;

            using (var scope = _scopes.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<ExpiredRequestsService>();

                var found = await service.FindStaleAsync(_inactiveDays, cancellationToken);
                if (!found.TryGetValue(out var list))
                {
                    return Result<ExpiredRequestsResult>.Fail(found.Errors);
                }

                ids = list;
            }

            var cancelled = 0;
            var failed = 0;

            foreach (var id in ids)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                using var scope = _scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ExpiredRequestsService>();

                var expired = await service.ExpireAsync(id, _inactiveDays, cancellationToken);

                if (!expired.TryGetValue(out var wasCancelled))
                {
                    failed++;
                }
                else if (wasCancelled)
                {
                    cancelled++;
                }
            }

            return new ExpiredRequestsResult(cancelled, failed);
        }
    }

}
