using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Admissions
{
    // The admission deadline of an academic year (academic-lifecycle.md section 6, open question: stored as a
    // system setting per academic year). Until the Calendar module exposes it, the Infrastructure
    // implementation reads configuration. A year without a deadline returns ADMISSION_DEADLINE_NOT_CONFIGURED,
    // which also stops applications for an unknown academic year.
    public interface IAdmissionDeadlineProvider
    {
        Task<Result<DateTimeOffset>> GetDeadlineAsync(Guid academicYearId, CancellationToken cancellationToken = default);
    }
}
