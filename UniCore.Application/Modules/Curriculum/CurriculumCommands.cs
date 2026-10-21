using UniCore.Domain.Modules.Curriculum;

namespace UniCore.Application.Modules.Curriculum
{
    // Nullable fields: a missing value is reported by the domain as a validation error instead of becoming a default.
    public sealed record CreateStudyPlanCommand(Guid AcademicProgramId, string? Code, string? Name, string? Description);
    public sealed record UpdateStudyPlanCommand(string? Name, string? Description);

    // SourceVersionId: copy this version (courses and requirements) into the new draft. Null starts an empty draft.
    public sealed record CreateVersionCommand(Guid? SourceVersionId, DateOnly? EffectiveFrom);
    public sealed record UpdateVersionCommand(DateOnly? EffectiveFrom);
    public sealed record RetireVersionCommand(DateOnly? EffectiveTo);

    public sealed record AddPlanCourseCommand(Guid CourseVersionId, Guid? AcademicLevelId, Guid? RecommendedTermId, CourseRequirementType? RequirementType, int? RequiredCredits);
    public sealed record UpdatePlanCourseCommand(Guid? AcademicLevelId, Guid? RecommendedTermId, CourseRequirementType? RequirementType, int? RequiredCredits);
    public sealed record SetRequirementCommand(Guid CourseVersionId, CourseRequirementType? RequirementType);
}
