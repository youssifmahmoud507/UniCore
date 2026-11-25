using UniCore.Application.Modules.Transcripts;
using UniCore.Domain.Modules.Graduation;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Graduation
{
    public sealed record GraduationApplicationSummary(
        Guid Id, Guid StudentId, Guid AcademicProgramId, Guid StudyPlanVersionId, GraduationApplicationStatus Status, DateTimeOffset SubmittedAt,
        decimal? CumulativeGpa, int? EarnedCredits, AcademicStanding? Standing, Guid? DecidedByUserId, DateTimeOffset? DecidedAt, string? DecisionNote, DateTimeOffset? CancelledAt);

    public sealed record UnmetRequirementSummary(string Code, string Message);

    public sealed record GraduationEligibilitySummary(
        Guid StudentId, bool IsEligible, GpaFigures Cumulative, IReadOnlyList<UnmetRequirementSummary> Unmet, IReadOnlyList<string> MissingRequiredCourses);

    public static class GraduationMapper
    {
        public static GraduationApplicationSummary ToSummary(GraduationApplication a)
            => new(a.Id, a.StudentId, a.AcademicProgramId, a.StudyPlanVersionId, a.Status, a.SubmittedAt, a.CumulativeGpa, a.EarnedCredits, a.Standing, a.DecidedByUserId, a.DecidedAt, a.DecisionNote, a.CancelledAt);

        public static GraduationEligibilitySummary ToSummary(GraduationEligibility e)
            => new(e.StudentId, e.IsEligible, e.Cumulative, [.. e.Unmet.Select(u => new UnmetRequirementSummary(u.Code, u.Description))], e.MissingRequiredCourses);
    }
}
