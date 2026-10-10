using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;

namespace UniCore.Infrastructure.Modules.Email
{
    public sealed class EmailDispatchService : BackgroundService
    {
        private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10) };

        private readonly EmailQueue _queue;
        private readonly IEmailSender _sender;
        private readonly ILogger<EmailDispatchService> _logger;

        public EmailDispatchService(EmailQueue queue, IEmailSender sender, ILogger<EmailDispatchService> logger)
        {
            _queue = queue;
            _sender = sender;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
                {
                    await DispatchAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // The application is shutting down.
            }
        }

        private async Task DispatchAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            for (var attempt = 0; ; attempt++)
            {
                var result = await _sender.SendAsync(message, cancellationToken);
                if (result.IsSuccess)
                {
                    return;
                }

                // Only temporary problems are worth retrying (a bad address will never work).
                var retryable = result.Error?.ErrorType == ErrorType.External;

                if (!retryable || attempt >= RetryDelays.Length)
                {
                    _logger.LogError(
                        "An email could not be delivered after {Attempts} attempt(s): {ErrorCode}",
                        attempt + 1, result.Error?.Code);
                    return;
                }

                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
        }
    }



}
