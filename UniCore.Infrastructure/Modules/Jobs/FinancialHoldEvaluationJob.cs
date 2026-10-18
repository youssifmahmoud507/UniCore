using Hangfire;
using Microsoft.Extensions.Logging;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Finance;

namespace UniCore.Infrastructure.Modules.Jobs
{
    // Every day: place holds on students with overdue invoices and release the ones who paid.
    public sealed class FinancialHoldEvaluationJob(IFinancialHoldRunner runner, IClock clock, ILogger<FinancialHoldEvaluationJob> logger)
    {
        public const string RecurringJobId = "financial-hold-evaluation";

        private readonly IFinancialHoldRunner _runner = runner;
        private readonly IClock _clock = clock;
        private readonly ILogger<FinancialHoldEvaluationJob> _logger = logger;

        // No automatic Hangfire retry: tomorrow's run does the same work.
        [AutomaticRetry(Attempts = 0)]
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var result = await _runner.RunAsync(_clock.UtcToday, cancellationToken);

            if (result.TryGetValue(out var outcome))
            {
                _logger.LogInformation("Financial holds evaluated: {Placed} placed, {Released} released, {Failed} failed.", outcome.Placed, outcome.Released, outcome.Failed);
            }
            else
            {
                _logger.LogWarning("Financial hold evaluation failed ({ErrorCode}).", result.Error?.Code);
            }
        }
    }


}
