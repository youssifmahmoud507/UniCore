using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Admissions.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Admissions;

namespace UniCore.Application.Modules.Admissions
{
    // The job side of "application past its deadline becomes Expired". There is no caller, so no permission
    // check: the Hangfire job (AdmissionExpiryJob) is the only entry point. Safe to run twice: an expired
    // application is no longer picked up.
    public sealed class AdmissionExpiryService(IAggregateRepository<AdmissionApplication> applications, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        public const int BatchSize = 100;
        public const int MaxBatchesPerRun = 50;

        private readonly IAggregateRepository<AdmissionApplication> _applications = applications;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result<ExpiryOutcome>> RunAsync(CancellationToken cancellationToken = default)
        {
            var expired = 0;
            var failed = 0;

            for (var batch = 0; batch < MaxBatchesPerRun; batch++)
            {
                var now = _clock.UtcNow;
                var overdue = await _applications.ListAsync(new OverdueApplicationsSpecification(now, BatchSize), cancellationToken);

                if (overdue.Count == 0)
                {
                    break;
                }

                var changed = new List<(AdmissionApplication Application, string Before)>();

                foreach (var application in overdue)
                {
                    var before = $"Status={application.Status}";
                    var result = application.Expire(now);

                    if (result.IsSuccess)
                    {
                        changed.Add((application, before));
                    }
                    else
                    {
                        failed++;
                    }
                }

                // Nothing could be expired in this batch: stop, or the same rows would be read again forever.
                if (changed.Count == 0)
                {
                    break;
                }

                var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
                if (saved.IsFailure)
                {
                    return Result<ExpiryOutcome>.Fail(saved.Errors);
                }

                foreach (var (application, before) in changed)
                {
                    await _audit.WriteAsync(new AuditEntry(AdmissionsAuditActions.ApplicationExpired, null, "AdmissionApplication", application.Id.ToString(), OldValues: before, NewValues: $"Status={application.Status}"), cancellationToken);
                }

                expired += changed.Count;
            }

            return new ExpiryOutcome(expired, failed);
        }
    }
}
