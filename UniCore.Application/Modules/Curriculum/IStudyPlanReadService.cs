using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Curriculum;

namespace UniCore.Application.Modules.Curriculum
{
    public sealed record StudyPlanVersionInfo(Guid VersionId, Guid StudyPlanId, Guid AcademicProgramId, int VersionNumber, StudyPlanVersionStatus Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

    // RequirementType is the effective one: the CourseRequirement row when there is one, otherwise the plan course's own type.
    public sealed record PlanCourseInfo(Guid CourseVersionId, CourseRequirementType RequirementType, Guid? AcademicLevelId, Guid? RecommendedTermId, int? RequiredCredits);

    // The read contract other modules use (my side of the integration): Registration asks "is this course in the
    // student's plan?", Graduation asks "which courses are required?", Students asks "does this version belong to
    // the program?". No permission checks: callers are other application services, not users.
    public interface IStudyPlanReadService
    {
        Task<Result<StudyPlanVersionInfo>> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default);

        // The version of a plan that is effective on a date (Active or Retired, never Draft).
        Task<Result<StudyPlanVersionInfo>> GetEffectiveVersionAsync(Guid studyPlanId, DateOnly date, CancellationToken cancellationToken = default);

        Task<Result<bool>> ContainsCourseAsync(Guid versionId, Guid courseVersionId, CancellationToken cancellationToken = default);

        Task<Result<IReadOnlyList<PlanCourseInfo>>> GetCoursesAsync(Guid versionId, CancellationToken cancellationToken = default);

        // For students: the version exists, is Active, and belongs to the program.
        Task<Result> VerifyVersionForProgramAsync(Guid versionId, Guid academicProgramId, CancellationToken cancellationToken = default);
    }
}
