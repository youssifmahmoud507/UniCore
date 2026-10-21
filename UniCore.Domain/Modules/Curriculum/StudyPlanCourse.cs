using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Curriculum
{
    // A course placed in one version of a plan. Only StudyPlanVersion creates and changes it.
    // The course is referenced by CourseVersionId (not CourseId), so a plan keeps the exact version it was built with.
    public sealed class StudyPlanCourse : BaseEntity
    {
        public const int MaxCredits = 30;

        private StudyPlanCourse()
        {
        }

        public Guid StudyPlanVersionId { get; private set; }
        public Guid CourseVersionId { get; private set; }

        // Where the plan recommends taking the course (both optional).
        public Guid? AcademicLevelId { get; private set; }
        public Guid? RecommendedTermId { get; private set; }

        public CourseRequirementType RequirementType { get; private set; }

        // Credits the plan counts for this course when they differ from the course version's own credits.
        // Null means "use the course version's credits".
        public int? RequiredCredits { get; private set; }

        internal static Result<StudyPlanCourse> Create(Guid versionId, Guid courseVersionId, Guid? academicLevelId, Guid? recommendedTermId, CourseRequirementType requirementType, int? requiredCredits)
        {
            var course = new StudyPlanCourse { Id = Guid.NewGuid(), StudyPlanVersionId = versionId, CourseVersionId = courseVersionId };

            if (courseVersionId == Guid.Empty)
            {
                return CurriculumErrors.CourseVersionInvalid;
            }

            var applied = course.Apply(academicLevelId, recommendedTermId, requirementType, requiredCredits);
            if (applied.IsFailure)
            {
                return Result<StudyPlanCourse>.Fail(applied.Errors);
            }

            return course;
        }

        internal Result Apply(Guid? academicLevelId, Guid? recommendedTermId, CourseRequirementType requirementType, int? requiredCredits)
        {
            if (!Enum.IsDefined(requirementType))
            {
                return Result.Fail(CurriculumErrors.RequirementTypeInvalid);
            }

            if (requiredCredits is { } credits && (credits < 1 || credits > MaxCredits))
            {
                return Result.Fail(CurriculumErrors.RequiredCreditsInvalid);
            }

            AcademicLevelId = academicLevelId == Guid.Empty ? null : academicLevelId;
            RecommendedTermId = recommendedTermId == Guid.Empty ? null : recommendedTermId;
            RequirementType = requirementType;
            RequiredCredits = requiredCredits;
            return Result.Ok();
        }

        internal StudyPlanCourse CopyTo(Guid versionId) => new()
        {
            Id = Guid.NewGuid(),
            StudyPlanVersionId = versionId,
            CourseVersionId = CourseVersionId,
            AcademicLevelId = AcademicLevelId,
            RecommendedTermId = RecommendedTermId,
            RequirementType = RequirementType,
            RequiredCredits = RequiredCredits
        };
    }
}
