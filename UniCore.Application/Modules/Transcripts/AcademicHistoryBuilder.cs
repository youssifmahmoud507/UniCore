using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Grading;
using UniCore.Application.Common.Repositories;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Transcripts;

namespace UniCore.Application.Modules.Transcripts
{
    // The student's academic history as calculated now: the published grades (IGradeReadService), the semesters' dates and labels,
    // the course codes and titles. Everything else (GPA, standing) comes from GpaCalculator. Reads only; no permission check.
    public sealed record HistoryCourse(GradeAttempt Attempt, string CourseCode, string CourseTitle, bool IsCounted);

    public sealed record HistorySemester(Guid SemesterId, string Label, DateOnly StartDate, GpaSummary Semester, GpaSummary Cumulative, IReadOnlyList<HistoryCourse> Courses);

    public sealed record AcademicHistory(GpaPolicy Policy, GpaSummary Cumulative, IReadOnlyList<HistorySemester> Semesters);

    public sealed class AcademicHistoryBuilder(
        IGradeReadService grades, ICourseCatalogReadService catalog,
        IRepository<AcademicSemester> semesters, IRepository<AcademicYear> years, IRepository<AcademicTerm> terms)
    {
        private readonly IGradeReadService _grades = grades;
        private readonly ICourseCatalogReadService _catalog = catalog;
        private readonly IRepository<AcademicSemester> _semesters = semesters;
        private readonly IRepository<AcademicYear> _years = years;
        private readonly IRepository<AcademicTerm> _terms = terms;

        public async Task<Result<AcademicHistory>> BuildAsync(Guid studentId, GpaPolicy policy, CancellationToken cancellationToken)
        {
            var published = await _grades.GetPublishedGradesAsync(studentId, null, cancellationToken);
            if (!published.TryGetValue(out var records))
            {
                return Result<AcademicHistory>.Fail(published.Errors);
            }

            var info = new Dictionary<Guid, (DateOnly Start, string Label)>();
            foreach (var semesterId in records.Select(r => r.AcademicSemesterId).Distinct())
            {
                var semester = await _semesters.GetByIdAsync(semesterId, cancellationToken);
                if (semester is null)
                {
                    return TranscriptErrors.SemesterNotFound;
                }

                var year = await _years.GetByIdAsync(semester.AcademicYearId, cancellationToken);
                var term = await _terms.GetByIdAsync(semester.AcademicTermId, cancellationToken);
                var label = year is not null && term is not null
                    ? $"{year.Name} - {term.Name}"
                    : $"Semester starting {semester.StartDate:yyyy-MM-dd}";

                info[semesterId] = (semester.StartDate, label);
            }

            var courses = new Dictionary<Guid, (string Code, string Title)>();
            foreach (var courseId in records.Select(r => r.CourseId).Distinct())
            {
                var course = await _catalog.GetCourseAsync(courseId, cancellationToken);
                if (!course.TryGetValue(out var found))
                {
                    return TranscriptErrors.CourseNotFound;
                }

                courses[courseId] = (found.Code, found.Title);
            }

            var attempts = records
                .Select(r => new GradeAttempt(r.GradeId, r.CourseId, r.AcademicSemesterId, info[r.AcademicSemesterId].Start, r.Credits, r.Percentage, r.Letter, r.GradePoints, r.IsPassing, r.PublishedAt))
                .ToList();

            var cumulative = GpaCalculator.Summarize(attempts, policy);
            var semesterList = GpaCalculator.BySemester(attempts, policy)
                .Select(s => new HistorySemester(
                    s.SemesterId, info[s.SemesterId].Label, s.SemesterStart, s.Semester, s.Cumulative,
                    [.. s.Outcomes.OrderBy(o => courses[o.Attempt.CourseId].Code, StringComparer.Ordinal)
                        .Select(o => new HistoryCourse(o.Attempt, courses[o.Attempt.CourseId].Code, courses[o.Attempt.CourseId].Title, o.IsCounted))]))
                .ToList();

            return new AcademicHistory(policy, cumulative, semesterList);
        }
    }
}
