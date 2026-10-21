using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Curriculum
{
    // The graduation rule for one course of a version: how the graduation check must treat it.
    // A course of the plan with no row here is treated by its StudyPlanCourse.RequirementType; a row here
    // overrides it (for example an Elective course of the plan that is Required for this version).
    // The graduation module (graduation-rules.md) reads these rows through IStudyPlanReadService.
    public sealed class CourseRequirement : BaseEntity
    {
        private CourseRequirement()
        {
        }

        public Guid StudyPlanVersionId { get; private set; }
        public Guid CourseVersionId { get; private set; }
        public CourseRequirementType RequirementType { get; private set; }

        internal static Result<CourseRequirement> Create(Guid versionId, Guid courseVersionId, CourseRequirementType requirementType)
        {
            if (courseVersionId == Guid.Empty)
            {
                return CurriculumErrors.CourseVersionInvalid;
            }

            if (!Enum.IsDefined(requirementType))
            {
                return CurriculumErrors.RequirementTypeInvalid;
            }

            return new CourseRequirement { Id = Guid.NewGuid(), StudyPlanVersionId = versionId, CourseVersionId = courseVersionId, RequirementType = requirementType };
        }

        internal Result ChangeType(CourseRequirementType requirementType)
        {
            if (!Enum.IsDefined(requirementType))
            {
                return Result.Fail(CurriculumErrors.RequirementTypeInvalid);
            }

            RequirementType = requirementType;
            return Result.Ok();
        }

        internal CourseRequirement CopyTo(Guid versionId) => new() { Id = Guid.NewGuid(), StudyPlanVersionId = versionId, CourseVersionId = CourseVersionId, RequirementType = RequirementType };
    }
}
