using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using UniCore.Application.Modules.Reporting;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Attendance;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.Reporting;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Reporting
{
    // Read-only, aggregated queries for the academic reports. Everything is AsNoTracking and grouped by the database
    // (GROUP BY); only the GPA report reads one small row per student. Before a query runs, the rows it would scan are
    // counted (the date filters are indexed), and a source over MaxSourceRows fails with REPORT_SOURCE_TOO_LARGE.
    public sealed class AcademicReportDataSource(AppDbContext db, ILogger<AcademicReportDataSource> logger) : IAcademicReportData
    {
        public const int MaxSourceRows = 200_000;

        private readonly AppDbContext _db = db;
        private readonly ILogger<AcademicReportDataSource> _logger = logger;

        public Task<Result<IReadOnlyList<EnrollmentStat>>> GetEnrollmentStatsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default) => ReadAsync<EnrollmentStat>(async () =>
            {
                var enrollments = _db.Set<Enrollment>().AsNoTracking().AsQueryable();

                if (from is { } start)
                {
                    enrollments = enrollments.Where(e => e.RegisteredAt >= start);
                }

                if (toExclusive is { } end)
                {
                    enrollments = enrollments.Where(e => e.RegisteredAt < end);
                }

                if (await enrollments.CountAsync(cancellationToken) > MaxSourceRows)
                {
                    return ReportingErrors.SourceTooLarge;
                }

                var groups = await enrollments
                    .Join(_db.Set<Course>().AsNoTracking(), e => e.CourseId, c => c.Id, (e, c) => new { c.DepartmentId, e.Status, e.Credits })
                    .Join(_db.Set<Department>().AsNoTracking(), x => x.DepartmentId, d => d.Id, (x, d) => new { x.DepartmentId, d.Name, x.Status, x.Credits })
                    .GroupBy(x => new { x.DepartmentId, x.Name, x.Status })
                    .Select(g => new { g.Key.DepartmentId, g.Key.Name, g.Key.Status, Count = g.Count(), Credits = g.Sum(x => x.Credits) })
                    .ToListAsync(cancellationToken);

                return Result<IReadOnlyList<EnrollmentStat>>.Ok([.. groups.Select(g => new EnrollmentStat(g.DepartmentId, g.Name, g.Status, g.Count, g.Credits))]);
            });

        public Task<Result<IReadOnlyList<GradeStat>>> GetGradeStatsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default) => ReadAsync<GradeStat>(async () =>
            {
                var grades = FinalGrades(from, toExclusive).Where(g => g.Letter != null);

                if (await grades.CountAsync(cancellationToken) > MaxSourceRows)
                {
                    return ReportingErrors.SourceTooLarge;
                }

                var groups = await grades
                    .Join(_db.Set<Course>().AsNoTracking(), g => g.CourseId, c => c.Id, (g, c) => new { c.DepartmentId, g.Letter, g.Percentage })
                    .Join(_db.Set<Department>().AsNoTracking(), x => x.DepartmentId, d => d.Id, (x, d) => new { x.DepartmentId, d.Name, x.Letter, x.Percentage })
                    .GroupBy(x => new { x.DepartmentId, x.Name, x.Letter })
                    .Select(g => new { g.Key.DepartmentId, g.Key.Name, g.Key.Letter, Count = g.Count(), PercentageSum = g.Sum(x => x.Percentage ?? 0m) })
                    .ToListAsync(cancellationToken);

                return Result<IReadOnlyList<GradeStat>>.Ok([.. groups.Select(g => new GradeStat(g.DepartmentId, g.Name, g.Letter ?? string.Empty, g.Count, g.PercentageSum))]);
            });

        public Task<Result<IReadOnlyList<StudentGpaStat>>> GetStudentGpaStatsAsync(DateTimeOffset? from, DateTimeOffset? toExclusive, CancellationToken cancellationToken = default) => ReadAsync<StudentGpaStat>(async () =>
            {
                var grades = FinalGrades(from, toExclusive).Where(g => g.GradePoints != null);

                if (await grades.CountAsync(cancellationToken) > MaxSourceRows)
                {
                    return ReportingErrors.SourceTooLarge;
                }

                var perStudent = grades
                    .GroupBy(g => g.StudentId)
                    .Select(g => new { StudentId = g.Key, QualityPoints = g.Sum(x => (x.GradePoints ?? 0m) * x.Credits), Credits = g.Sum(x => x.Credits) });

                var programs = _db.Set<StudentProgramHistory>().AsNoTracking();

                // The department of the student's latest program (a graduated student's program is closed but still the latest).
                var rows = await perStudent
                    .Join(programs, s => s.StudentId, h => h.StudentId, (s, h) => new { s, h })
                    .Where(x => x.h.StartDate == programs.Where(o => o.StudentId == x.h.StudentId).Max(o => o.StartDate))
                    .Join(_db.Set<AcademicProgram>().AsNoTracking(), x => x.h.AcademicProgramId, p => p.Id, (x, p) => new { x.s, p.DepartmentId })
                    .Join(_db.Set<Department>().AsNoTracking(), x => x.DepartmentId, d => d.Id, (x, d) => new { x.DepartmentId, d.Name, x.s.QualityPoints, x.s.Credits })
                    .Take(MaxSourceRows + 1)
                    .ToListAsync(cancellationToken);

                if (rows.Count > MaxSourceRows)
                {
                    return ReportingErrors.SourceTooLarge;
                }

                return Result<IReadOnlyList<StudentGpaStat>>.Ok([.. rows.Select(r => new StudentGpaStat(r.DepartmentId, r.Name, r.QualityPoints, r.Credits))]);
            });

        public Task<Result<IReadOnlyList<AttendanceStat>>> GetAttendanceStatsAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default) => ReadAsync<AttendanceStat>(async () =>
            {
                var sessions = _db.Set<AttendanceSession>().AsNoTracking().Where(s => s.Status == AttendanceSessionStatus.Closed);

                if (from is { } start)
                {
                    sessions = sessions.Where(s => s.SessionDate >= start);
                }

                if (to is { } end)
                {
                    sessions = sessions.Where(s => s.SessionDate <= end);
                }

                var records = _db.Set<AttendanceRecord>().AsNoTracking()
                    .Join(sessions, r => r.AttendanceSessionId, s => s.Id, (r, s) => new { r.EnrollmentId, r.Status });

                if (await records.CountAsync(cancellationToken) > MaxSourceRows)
                {
                    return ReportingErrors.SourceTooLarge;
                }

                var groups = await records
                    .Join(_db.Set<Enrollment>().AsNoTracking(), r => r.EnrollmentId, e => e.Id, (r, e) => new { e.CourseId, r.Status })
                    .Join(_db.Set<Course>().AsNoTracking(), x => x.CourseId, c => c.Id, (x, c) => new { c.DepartmentId, x.Status })
                    .Join(_db.Set<Department>().AsNoTracking(), x => x.DepartmentId, d => d.Id, (x, d) => new { x.DepartmentId, d.Name, x.Status })
                    .GroupBy(x => new { x.DepartmentId, x.Name, x.Status })
                    .Select(g => new { g.Key.DepartmentId, g.Key.Name, g.Key.Status, Count = g.Count() })
                    .ToListAsync(cancellationToken);

                return Result<IReadOnlyList<AttendanceStat>>.Ok([.. groups.Select(g => new AttendanceStat(g.DepartmentId, g.Name, g.Status.ToString(), g.Count))]);
            });

        // Published and Locked grades published in the range: the only grades a student can see as final.
        private IQueryable<Grade> FinalGrades(DateTimeOffset? from, DateTimeOffset? toExclusive)
        {
            var grades = _db.Set<Grade>().AsNoTracking().Where(g => g.Status == GradeStatus.Published || g.Status == GradeStatus.Locked);

            if (from is { } start)
            {
                grades = grades.Where(g => g.PublishedAt >= start);
            }

            if (toExclusive is { } end)
            {
                grades = grades.Where(g => g.PublishedAt < end);
            }

            return grades;
        }

        // Database trouble is a Result, never an exception, and the details stay in the log.
        private async Task<Result<IReadOnlyList<T>>> ReadAsync<T>(Func<Task<Result<IReadOnlyList<T>>>> read)
        {
            try
            {
                return await read();
            }
            catch (OperationCanceledException)
            {
                return CommonErrors.OperationCancelled;
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException)
            {
                _logger.LogError("An academic report query failed ({ExceptionType}).", ex.GetType().Name);
                return ReportingErrors.DataUnavailable;
            }
        }
    }
}
