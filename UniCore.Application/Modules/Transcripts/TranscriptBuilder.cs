using UniCore.Domain.Modules.Transcripts;

namespace UniCore.Application.Modules.Transcripts
{
    // Turns the calculated history into the read models and into the data an official snapshot freezes.
    public static class TranscriptBuilder
    {
        public static AcademicHistorySummary ToHistory(Guid studentId, string studentNumber, AcademicHistory h)
            => new(studentId, studentNumber, h.Policy.Repeat, TranscriptMapper.ToFigures(h.Cumulative), h.Cumulative.ReplacedAttempts, ToSemesters(h));

        public static TranscriptSummary ToUnofficial(Guid studentId, string studentNumber, AcademicHistory h, DateTimeOffset now)
            => new(null, null, false, studentId, studentNumber, now, h.Policy.Repeat, TranscriptMapper.ToFigures(h.Cumulative), h.Cumulative.ReplacedAttempts, ToSemesters(h));

        public static IReadOnlyList<TranscriptEntryData> ToEntries(AcademicHistory h)
            => [.. h.Semesters.SelectMany(s => s.Courses.Select(c => new TranscriptEntryData(
                c.Attempt.GradeId, s.SemesterId, s.Label, s.StartDate, c.Attempt.CourseId, c.CourseCode, c.CourseTitle, c.Attempt.Credits,
                c.Attempt.Percentage, c.Attempt.Letter, c.Attempt.GradePoints, c.Attempt.IsPassing, c.IsCounted, s.Semester.Gpa, s.Cumulative.Gpa)))];

        private static IReadOnlyList<TranscriptSemesterSummary> ToSemesters(AcademicHistory h)
            => [.. h.Semesters.Select(s => new TranscriptSemesterSummary(
                s.SemesterId, s.Label, s.StartDate, TranscriptMapper.ToFigures(s.Semester), TranscriptMapper.ToFigures(s.Cumulative),
                [.. s.Courses.Select(c => new TranscriptCourseSummary(c.Attempt.GradeId, c.Attempt.CourseId, c.CourseCode, c.CourseTitle, c.Attempt.Credits, c.Attempt.Percentage, c.Attempt.Letter, c.Attempt.GradePoints, c.Attempt.IsPassing, c.IsCounted))]))];
    }
}
