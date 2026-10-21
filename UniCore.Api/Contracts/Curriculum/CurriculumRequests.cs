using UniCore.Domain.Modules.Curriculum;

namespace UniCore.Api.Contracts.Curriculum
{
    // Ids, numbers and enums are nullable so a missing value is reported as a validation error instead of silently becoming a default.
    public sealed record CreateStudyPlanRequest(Guid? AcademicProgramId, string? Code, string? Name, string? Description);
    public sealed record UpdateStudyPlanRequest(string? Name, string? Description);

    public sealed record CreateVersionRequest(Guid? SourceVersionId, DateOnly? EffectiveFrom);
    public sealed record UpdateVersionRequest(DateOnly? EffectiveFrom);
    public sealed record RetireVersionRequest(DateOnly? EffectiveTo);

    public sealed record AddPlanCourseRequest(Guid? CourseVersionId, Guid? AcademicLevelId, Guid? RecommendedTermId, CourseRequirementType? RequirementType, int? RequiredCredits);
    public sealed record UpdatePlanCourseRequest(Guid? AcademicLevelId, Guid? RecommendedTermId, CourseRequirementType? RequirementType, int? RequiredCredits);
    public sealed record SetRequirementRequest(Guid? CourseVersionId, CourseRequirementType? RequirementType);
}
