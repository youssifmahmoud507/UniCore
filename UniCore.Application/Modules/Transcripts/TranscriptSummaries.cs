using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.Transcripts;

namespace UniCore.Application.Modules.Transcripts
{
    public sealed record TranscriptCourseSummary(
        Guid GradeId, Guid CourseId, string CourseCode, string CourseTitle, int Credits, decimal Percentage, string Letter, decimal GradePoints, bool IsPassing, bool IsCounted);

    public sealed record TranscriptSemesterSummary(
        Guid SemesterId, string Label, DateOnly StartDate, GpaFigures Semester, GpaFigures CumulativeAtEnd, IReadOnlyList<TranscriptCourseSummary> Courses);

    // The AcademicHistory read model: the student's semesters with their courses, GPAs and standing as it was at the end of each.
    public sealed record AcademicHistorySummary(
        Guid StudentId, string StudentNumber, RepeatPolicy RepeatPolicy, GpaFigures Cumulative, int ReplacedAttempts, IReadOnlyList<TranscriptSemesterSummary> Semesters);

    // The Transcript read model. Unofficial (calculated now, SnapshotId and VerificationCode null) or official (a frozen snapshot).
    public sealed record TranscriptSummary(
        Guid? SnapshotId, string? VerificationCode, bool IsOfficial, Guid StudentId, string StudentNumber, DateTimeOffset GeneratedAt,
        RepeatPolicy RepeatPolicy, GpaFigures Cumulative, int ReplacedAttempts, IReadOnlyList<TranscriptSemesterSummary> Semesters);

    public sealed record TranscriptSnapshotListItem(Guid SnapshotId, string VerificationCode, DateTimeOffset IssuedAt, decimal CumulativeGpa, int EarnedCredits, AcademicStanding? Standing);

    public static class TranscriptMapper
    {
        public static GpaFigures ToFigures(GpaSummary s) => new(s.Gpa, s.AttemptedCredits, s.EarnedCredits, s.Standing);

        public static TranscriptSnapshotListItem ToListItem(TranscriptSnapshot s) => new(s.Id, s.VerificationCode, s.IssuedAt, s.CumulativeGpa, s.EarnedCredits, s.Standing);

        // An official transcript read back from its frozen entries: semesters in date order, courses in the order they were frozen.
        public static TranscriptSummary ToSummary(TranscriptSnapshot s)
        {
            var semesters = s.Entries
                .GroupBy(e => new { e.SemesterId, e.SemesterLabel, e.SemesterStart })
                .OrderBy(g => g.Key.SemesterStart)
                .Select(g =>
                {
                    var first = g.First();
                    var counted = g.Where(e => e.IsCounted).ToList();

                    return new TranscriptSemesterSummary(
                        g.Key.SemesterId, g.Key.SemesterLabel, g.Key.SemesterStart,
                        new GpaFigures(first.SemesterGpa, counted.Sum(e => e.Credits), counted.Where(e => e.IsPassing).Sum(e => e.Credits), null),
                        new GpaFigures(first.CumulativeGpa, 0, 0, null),
                        [.. g.OrderBy(e => e.SequenceNo).Select(e => new TranscriptCourseSummary(e.GradeId, e.CourseId, e.CourseCode, e.CourseTitle, e.Credits, e.Percentage, e.Letter, e.GradePoints, e.IsPassing, e.IsCounted))]);
                })
                .ToList();

            return new TranscriptSummary(
                s.Id, s.VerificationCode, true, s.StudentId, s.StudentNumber, s.IssuedAt, s.RepeatPolicy,
                new GpaFigures(s.CumulativeGpa, s.AttemptedCredits, s.EarnedCredits, s.Standing), s.Entries.Count(e => !e.IsCounted), semesters);
        }
    }
}
