using Hangfire;
using Microsoft.Extensions.Logging;

namespace UniCore.Infrastructure.Modules.Maintenance
{
    public sealed class ExpiredRequestsJob(ExpiredRequestsRunner runner, ILogger<ExpiredRequestsJob> logger)
    {
        public const string RecurringJobId = "expired-requests";

        private readonly ExpiredRequestsRunner _runner = runner;
        private readonly ILogger<ExpiredRequestsJob> _logger = logger;

        // No automatic Hangfire retry: tomorrow's run does the same work.
        [AutomaticRetry(Attempts = 0)]
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var result = await _runner.RunAsync(cancellationToken);

            if (result.TryGetValue(out var outcome))
            {
                _logger.LogInformation("Expired requests: {Cancelled} cancelled, {Failed} failed.", outcome.Cancelled, outcome.Failed);
            }
            else
            {
                _logger.LogWarning("Expiring requests failed ({ErrorCode}).", result.Error?.Code);
            }
        }
    }

}
