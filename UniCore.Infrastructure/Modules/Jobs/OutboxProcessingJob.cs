using Hangfire;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Infrastructure.Modules.Outbox;

namespace UniCore.Infrastructure.Modules.Jobs
{
    // Drains the outbox. Runs every minute and also right after new messages are saved.
    // No locking needed: the processor claims each message atomically.
    public sealed class OutboxProcessingJob(OutboxProcessor processor, IOutboxSignal signal, ILogger<OutboxProcessingJob> logger)
    {
        public const string RecurringJobId = "outbox-processing";

        private const int BatchSize = 50;
        private const int MaxBatchesPerRun = 20;

        private readonly OutboxProcessor _processor = processor;
        private readonly IOutboxSignal _signal = signal;
        private readonly ILogger<OutboxProcessingJob> _logger = logger;

        // No automatic Hangfire retry: a failed run is followed by the next scheduled one.
        [AutomaticRetry(Attempts = 0)]
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            _signal.Acknowledge();

            for (var batch = 0; batch < MaxBatchesPerRun; batch++)
            {
                var result = await _processor.ProcessBatchAsync(BatchSize, cancellationToken);

                if (!result.TryGetValue(out var processed))
                {
                    _logger.LogWarning("Outbox processing stopped ({ErrorCode}); the next run will try again.", result.Error?.Code);
                    return;
                }

                if (processed < BatchSize)
                {
                    return;
                }
            }
        }
    }



}
