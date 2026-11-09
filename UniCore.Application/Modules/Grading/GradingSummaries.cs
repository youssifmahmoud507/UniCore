using UniCore.Domain.Modules.Grading;

namespace UniCore.Application.Modules.Grading
{
    public sealed record GradeScoreSummary(Guid ComponentId, decimal Score);

    public sealed record GradeSummary(
        Guid Id, Guid EnrollmentId, Guid StudentId, Guid CourseSectionId, Guid AcademicSemesterId, Guid CourseId, int Credits, GradeStatus Status,
        IReadOnlyList<GradeScoreSummary> Scores, decimal? Percentage, string? Letter, decimal? GradePoints, bool? IsPassing,
        string? ReturnReason, DateTimeOffset? SubmittedAt, DateTimeOffset? PublishedAt, DateTimeOffset? LockedAt);

    // The result of a whole-section step (submit, return, review, approve, publish, lock).
    public sealed record SectionStepSummary(Guid CourseSectionId, string Step, GradeStatus Status, int Count);

    public sealed record GradeBandSummary(string Letter, decimal MinPercentage, decimal GradePoints, bool IsPassing);

    public sealed record GradeScaleSummary(Guid Id, string Name, bool IsDefault, IReadOnlyList<GradeBandSummary> Bands);

    public static class GradingMapper
    {
        public static GradeSummary ToSummary(Grade g)
            => new(
                g.Id, g.EnrollmentId, g.StudentId, g.CourseSectionId, g.AcademicSemesterId, g.CourseId, g.Credits, g.Status,
                [.. g.Scores.Select(s => new GradeScoreSummary(s.AssessmentComponentId, s.Score))],
                g.Percentage, g.Letter, g.GradePoints, g.IsPassing, g.ReturnReason, g.SubmittedAt, g.PublishedAt, g.LockedAt);

        public static GradeScaleSummary ToSummary(GradeScale s)
            => new(s.Id, s.Name, s.IsDefault, [.. s.Bands.OrderByDescending(b => b.MinPercentage).Select(b => new GradeBandSummary(b.Letter, b.MinPercentage, b.GradePoints, b.IsPassing))]);
    }
}
