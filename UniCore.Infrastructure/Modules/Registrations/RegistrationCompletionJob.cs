using Hangfire;
using Microsoft.Extensions.Logging;
using UniCore.Application.Modules.Registrations;

namespace UniCore.Infrastructure.Modules.Registrations
{
    // Every day: registrations of Closed semesters become Completed. Scoped (the service uses the DbContext).
    public sealed class RegistrationCompletionJob(RegistrationCompletionService service, ILogger<RegistrationCompletionJob> logger)
    {
        public const string RecurringJobId = "registration-completion";

        private readonly RegistrationCompletionService _service = service;
        private readonly ILogger<RegistrationCompletionJob> _logger = logger;

        // No automatic retry: tomorrow's run does the same work.
        [AutomaticRetry(Attempts = 0)]
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var result = await _service.RunAsync(cancellationToken);

            if (result.TryGetValue(out var outcome))
            {
                _logger.LogInformation("Registration completion: {Completed} completed, {Failed} failed.", outcome.Completed, outcome.Failed);
            }
            else
            {
                _logger.LogWarning("Registration completion failed ({ErrorCode}).", result.Error?.Code);
            }
        }
    }
}
