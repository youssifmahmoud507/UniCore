using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Registrations.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Registrations;

namespace UniCore.Application.Modules.Registrations
{
    public sealed record RegistrationCompletionOutcome(int Completed, int Failed);

    // Runs after a semester is Closed: every Draft or Submitted registration of that semester becomes Completed and its
    // registered enrollments become Completed. No user is involved, so it checks no permission and writes no audit
    // entry per row; the job logs the totals. Safe to run again: completed registrations are not selected.
    public sealed class RegistrationCompletionService(IRepository<AcademicSemester> semesters, IAggregateRepository<Registration> registrations, IUnitOfWork unitOfWork, IClock clock)
    {
        private readonly IRepository<AcademicSemester> _semesters = semesters;
        private readonly IAggregateRepository<Registration> _registrations = registrations;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;

        public async Task<Result<RegistrationCompletionOutcome>> RunAsync(CancellationToken cancellationToken = default)
        {
            var closed = await _semesters.ListAsync(new ClosedSemestersSpecification(), cancellationToken);
            var completed = 0;
            var failed = 0;

            foreach (var semester in closed)
            {
                var pending = await _registrations.ListAsync(new RegistrationsToCompleteSpecification(semester.Id), cancellationToken);
                var count = 0;

                foreach (var registration in pending)
                {
                    if (registration.Complete(_clock.UtcNow).IsSuccess)
                    {
                        _registrations.Update(registration);
                        count++;
                    }
                    else
                    {
                        failed++;
                    }
                }

                if (count == 0)
                {
                    continue;
                }

                // One save per semester: a conflict loses only that semester's batch, tomorrow's run retries it.
                var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
                if (saved.IsSuccess)
                {
                    completed += count;
                }
                else
                {
                    failed += count;
                }
            }

            return new RegistrationCompletionOutcome(completed, failed);
        }
    }
}
