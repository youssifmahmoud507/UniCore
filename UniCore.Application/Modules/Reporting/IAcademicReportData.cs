using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Reporting
{
    // What the four academic reports read. Implemented in Infrastructure with read-only, aggregated queries.
    // A call whose source is bigger than the limit (200,000 rows) fails with REPORT_SOURCE_TOO_LARGE instead of running.
    // Ranges: from is included, toExclusive is not (both ends optional).
    public interface IAcademicReportData
    {
        // Enrollments registered in the range, by department and status.
        Task<Result<IReadOnlyList<EnrollmentStat>>> GetEnrollmentStatsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default);

        // Published and Locked grades only, published in the range, by department and letter.
        Task<Result<IReadOnlyList<GradeStat>>> GetGradeStatsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default);

        // Published and Locked grades published in the range, summed per student (department of the student's latest program).
        Task<Result<IReadOnlyList<StudentGpaStat>>> GetStudentGpaStatsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default);

        // Marks of CLOSED sessions held in the range (both days included), by department and status.
        Task<Result<IReadOnlyList<AttendanceStat>>> GetAttendanceStatsAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    }
}
