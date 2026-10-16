using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Calendar.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;

namespace UniCore.Application.Modules.Calendar
{
    // "Today" in the university time zone. Window checks never compare raw DateTimes (academic-lifecycle.md section 3).
    public interface IAcademicClock
    {
        DateOnly Today { get; }
    }

    // The one place that answers "is this window open?". Registration, attendance and grading call it,
    // so no other module re-implements the date rules.
    public interface IAcademicCalendarService
    {
        Task<Result<SemesterSummary>> GetCurrentSemesterAsync(CancellationToken cancellationToken = default);
        Task<Result<bool>> IsRegistrationOpenAsync(Guid semesterId, CancellationToken cancellationToken = default);
        Task<Result<bool>> IsAddDropOpenAsync(Guid semesterId, CancellationToken cancellationToken = default);
        Task<Result<bool>> IsWithdrawalOpenAsync(Guid semesterId, CancellationToken cancellationToken = default);
        Task<Result<bool>> IsGradeSubmissionOpenAsync(Guid semesterId, CancellationToken cancellationToken = default);
        Task<Result<bool>> IsGradePublicationReachedAsync(Guid semesterId, CancellationToken cancellationToken = default);
    }

    public sealed class AcademicCalendarService(IRepository<AcademicYear> years, IRepository<AcademicSemester> semesters, IAcademicClock clock) : IAcademicCalendarService
    {
        private readonly IRepository<AcademicYear> _years = years;
        private readonly IRepository<AcademicSemester> _semesters = semesters;
        private readonly IAcademicClock _clock = clock;

        // The current semester is the semester of the current year whose date range contains today.
        public async Task<Result<SemesterSummary>> GetCurrentSemesterAsync(CancellationToken cancellationToken = default)
        {
            var year = await _years.FirstOrDefaultAsync(new CurrentYearSpecification(null), cancellationToken);
            if (year is null)
            {
                return CalendarErrors.NoCurrentSemester;
            }

            var semester = await _semesters.FirstOrDefaultAsync(new SemestersContainingDateSpecification(year.Id, _clock.Today), cancellationToken);
            return semester is null ? CalendarErrors.NoCurrentSemester : CalendarMapper.ToSummary(semester);
        }

        public Task<Result<bool>> IsRegistrationOpenAsync(Guid semesterId, CancellationToken cancellationToken = default)
            => CheckAsync(semesterId, (s, today) => s.IsRegistrationOpen(today), cancellationToken);

        public Task<Result<bool>> IsAddDropOpenAsync(Guid semesterId, CancellationToken cancellationToken = default)
            => CheckAsync(semesterId, (s, today) => s.IsAddDropOpen(today), cancellationToken);

        public Task<Result<bool>> IsWithdrawalOpenAsync(Guid semesterId, CancellationToken cancellationToken = default)
            => CheckAsync(semesterId, (s, today) => s.IsWithdrawalOpen(today), cancellationToken);

        public Task<Result<bool>> IsGradeSubmissionOpenAsync(Guid semesterId, CancellationToken cancellationToken = default)
            => CheckAsync(semesterId, (s, today) => s.IsGradeSubmissionOpen(today), cancellationToken);

        public Task<Result<bool>> IsGradePublicationReachedAsync(Guid semesterId, CancellationToken cancellationToken = default)
            => CheckAsync(semesterId, (s, today) => s.IsGradePublicationReached(today), cancellationToken);

        private async Task<Result<bool>> CheckAsync(Guid semesterId, Func<AcademicSemester, DateOnly, bool> check, CancellationToken cancellationToken)
        {
            var semester = await _semesters.GetByIdAsync(semesterId, cancellationToken);
            if (semester is null)
            {
                return CalendarErrors.SemesterNotFound;
            }

            return check(semester, _clock.Today);
        }
    }
}
