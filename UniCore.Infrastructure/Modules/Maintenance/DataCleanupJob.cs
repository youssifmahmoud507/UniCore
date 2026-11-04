using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UniCore.Infrastructure.Modules.Maintenance
{
    public sealed class DataCleanupJob(IServiceScopeFactory scopes, ILogger<DataCleanupJob> logger)
    {
        public const string RecurringJobId = "data-cleanup";

        private readonly IServiceScopeFactory _scopes = scopes;
        private readonly ILogger<DataCleanupJob> _logger = logger;

        [AutomaticRetry(Attempts = 0)]
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopes.CreateScope();

            var result = await scope.ServiceProvider.GetRequiredService<DataCleanupService>().CleanAsync(cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning("Data cleanup failed ({ErrorCode}).", result.Error?.Code);
            }
        }
    }

}
