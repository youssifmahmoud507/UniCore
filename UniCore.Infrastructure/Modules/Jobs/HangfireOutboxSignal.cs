using Hangfire;
using Microsoft.Extensions.Logging;
using UniCore.Infrastructure.Modules.Outbox;

namespace UniCore.Infrastructure.Modules.Jobs
{
    // Queues one outbox run in Hangfire when new messages are saved.
    // Only one run is queued at a time: more signals do nothing until it starts.
    public sealed class HangfireOutboxSignal(IBackgroundJobClient jobs, ILogger<HangfireOutboxSignal> logger) : IOutboxSignal
    {
        private readonly IBackgroundJobClient _jobs = jobs;
        private readonly ILogger<HangfireOutboxSignal> _logger = logger;
        private int _queued;

        public void Signal()
        {
            if (Interlocked.Exchange(ref _queued, 1) == 1)
            {
                return;
            }

            try
            {
                _jobs.Enqueue<OutboxProcessingJob>(job => job.RunAsync(CancellationToken.None));
            }
            catch (Exception ex)
            {
                // Hangfire storage is down. The scheduled run still delivers the messages.
                Interlocked.Exchange(ref _queued, 0);
                _logger.LogWarning("The outbox job could not be queued ({ExceptionType}); the scheduled run will pick the messages up.", ex.GetType().Name);
            }
        }

        public void Acknowledge() => Interlocked.Exchange(ref _queued, 0);
    }



}
