using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UniCore.Infrastructure.Modules.Outbox
{
    public sealed class OutboxPollingService(OutboxProcessor processor, ILogger<OutboxPollingService> logger) : BackgroundService
    {
        private const int BatchSize = 50;
        private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(10);

        private readonly OutboxProcessor _processor = processor;
        private readonly ILogger<OutboxPollingService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var result = await _processor.ProcessBatchAsync(BatchSize, stoppingToken);

                    if (result.TryGetValue(out var processed))
                    {
                        if (processed >= BatchSize)
                        {
                            continue;
                        }

                        await Task.Delay(IdleDelay, stoppingToken);
                        continue;
                    }

                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    _logger.LogWarning("Outbox processing failed ({ErrorCode}); retrying later.", result.Error?.Code);
                    await Task.Delay(ErrorDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // The application is shutting down.
            }
        }
    }



}
