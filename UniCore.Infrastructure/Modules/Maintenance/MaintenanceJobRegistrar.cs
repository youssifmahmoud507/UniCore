using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UniCore.Infrastructure.Modules.Maintenance
{
    // Registers the two recurring jobs when the application starts. It never stops the application from starting:
    // if Hangfire storage is not ready it logs, and the jobs are registered on the next start.
    public sealed class MaintenanceJobRegistrar(IRecurringJobManager recurring, ILogger<MaintenanceJobRegistrar> logger) : IHostedService
    {
        private readonly IRecurringJobManager _recurring = recurring;
        private readonly ILogger<MaintenanceJobRegistrar> _logger = logger;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _recurring.AddOrUpdate<ExpiredRequestsJob>(ExpiredRequestsJob.RecurringJobId, job => job.RunAsync(CancellationToken.None), Cron.Daily(3, 30));
                _recurring.AddOrUpdate<DataCleanupJob>(DataCleanupJob.RecurringJobId, job => job.RunAsync(CancellationToken.None), Cron.Daily(4));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("The maintenance jobs were not registered ({ExceptionType}).", ex.GetType().Name);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

}
