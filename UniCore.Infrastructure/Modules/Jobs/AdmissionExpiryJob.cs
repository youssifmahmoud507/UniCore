using Hangfire;
using Microsoft.Extensions.Logging;
using UniCore.Application.Modules.Admissions;

namespace UniCore.Infrastructure.Modules.Jobs
{
    // Every day: applications past their admission deadline become Expired.
    // Scoped, not singleton: AdmissionExpiryService uses the DbContext.
    public sealed class AdmissionExpiryJob(AdmissionExpiryService service, ILogger<AdmissionExpiryJob> logger)
    {
        public const string RecurringJobId = "admission-expiry";

        private readonly AdmissionExpiryService _service = service;
        private readonly ILogger<AdmissionExpiryJob> _logger = logger;

        // No automatic retry: tomorrow's run does the same work.
        [AutomaticRetry(Attempts = 0)]
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var result = await _service.RunAsync(cancellationToken);

            if (result.TryGetValue(out var outcome))
            {
                _logger.LogInformation("Admission expiry: {Expired} expired, {Failed} failed.", outcome.Expired, outcome.Failed);
            }
            else
            {
                _logger.LogWarning("Admission expiry failed ({ErrorCode}).", result.Error?.Code);
            }
        }
    }
}
