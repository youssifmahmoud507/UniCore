using UniCore.Domain.Modules.Curriculum;

namespace UniCore.Application.Modules.Curriculum
{
    public sealed record StudyPlanSummary(Guid Id, Guid AcademicProgramId, string Code, string Name, string? Description, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    public sealed record VersionSummary(Guid Id, Guid StudyPlanId, int VersionNumber, DateOnly EffectiveFrom, DateOnly? EffectiveTo, StudyPlanVersionStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    public sealed record PlanCourseSummary(Guid Id, Guid CourseVersionId, Guid? AcademicLevelId, Guid? RecommendedTermId, CourseRequirementType RequirementType, int? RequiredCredits);

    public sealed record CourseRequirementSummary(Guid Id, Guid CourseVersionId, CourseRequirementType RequirementType);

    public sealed record VersionDetail(VersionSummary Version, IReadOnlyList<PlanCourseSummary> Courses, IReadOnlyList<CourseRequirementSummary> Requirements);
}
