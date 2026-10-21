using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Curriculum.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Curriculum;

namespace UniCore.Application.Modules.Curriculum
{
    // Implements the read contract for the other modules. No permission checks (the callers are services, not users).
    public sealed class StudyPlanReadService(IAggregateRepository<StudyPlanVersion> versions, IAggregateRepository<StudyPlan> plans) : IStudyPlanReadService
    {
        private readonly IAggregateRepository<StudyPlanVersion> _versions = versions;
        private readonly IAggregateRepository<StudyPlan> _plans = plans;

        private async Task<Result<(StudyPlanVersion Version, StudyPlan Plan)>> LoadAsync(Guid versionId, CancellationToken cancellationToken)
        {
            var version = await _versions.FirstOrDefaultAsync(new VersionWithDetailsSpecification(versionId, tracking: false), cancellationToken);
            if (version is null)
            {
                return CurriculumErrors.VersionNotFound;
            }

            var plan = await _plans.GetByIdAsync(version.StudyPlanId, cancellationToken);
            if (plan is null)
            {
                return CurriculumErrors.PlanNotFound;
            }

            return (version, plan);
        }

        private static StudyPlanVersionInfo ToInfo(StudyPlanVersion version, StudyPlan plan)
            => new(version.Id, plan.Id, plan.AcademicProgramId, version.VersionNumber, version.Status, version.EffectiveFrom, version.EffectiveTo);

        public async Task<Result<StudyPlanVersionInfo>> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(versionId, cancellationToken);

            return loaded.TryGetValue(out var found) ? ToInfo(found.Version, found.Plan) : Result<StudyPlanVersionInfo>.Fail(loaded.Errors);
        }

        public async Task<Result<StudyPlanVersionInfo>> GetEffectiveVersionAsync(Guid studyPlanId, DateOnly date, CancellationToken cancellationToken = default)
        {
            var plan = await _plans.GetByIdAsync(studyPlanId, cancellationToken);
            if (plan is null)
            {
                return CurriculumErrors.PlanNotFound;
            }

            var version = await _versions.FirstOrDefaultAsync(new EffectiveVersionSpecification(studyPlanId, date), cancellationToken);

            return version is null ? CurriculumErrors.VersionNotFound : ToInfo(version, plan);
        }

        public async Task<Result<bool>> ContainsCourseAsync(Guid versionId, Guid courseVersionId, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(versionId, cancellationToken);

            return loaded.TryGetValue(out var found) ? found.Version.ContainsCourse(courseVersionId) : Result<bool>.Fail(loaded.Errors);
        }

        public async Task<Result<IReadOnlyList<PlanCourseInfo>>> GetCoursesAsync(Guid versionId, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(versionId, cancellationToken);
            if (!loaded.TryGetValue(out var found))
            {
                return Result<IReadOnlyList<PlanCourseInfo>>.Fail(loaded.Errors);
            }

            var version = found.Version;

            IReadOnlyList<PlanCourseInfo> courses =
                [.. version.Courses.Select(c => new PlanCourseInfo(c.CourseVersionId, version.EffectiveRequirementTypeOf(c.CourseVersionId) ?? c.RequirementType, c.AcademicLevelId, c.RecommendedTermId, c.RequiredCredits))];

            return Result<IReadOnlyList<PlanCourseInfo>>.Ok(courses);
        }

        public async Task<Result> VerifyVersionForProgramAsync(Guid versionId, Guid academicProgramId, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(versionId, cancellationToken);
            if (!loaded.TryGetValue(out var found))
            {
                return Result.Fail(loaded.Errors);
            }

            if (found.Plan.AcademicProgramId != academicProgramId)
            {
                return Result.Fail(CurriculumErrors.ProgramMismatch);
            }

            return found.Version.Status == StudyPlanVersionStatus.Active ? Result.Ok() : Result.Fail(CurriculumErrors.VersionNotActive);
        }
    }
}
