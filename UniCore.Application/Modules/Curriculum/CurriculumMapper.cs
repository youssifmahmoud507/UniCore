using UniCore.Domain.Modules.Curriculum;

namespace UniCore.Application.Modules.Curriculum
{
    // Entity -> DTO. The entities are never returned to the API directly.
    public static class CurriculumMapper
    {
        public static StudyPlanSummary ToSummary(StudyPlan p)
            => new(p.Id, p.AcademicProgramId, p.Code, p.Name, p.Description, p.IsActive, p.CreatedAt, p.UpdatedAt);

        public static VersionSummary ToSummary(StudyPlanVersion v)
            => new(v.Id, v.StudyPlanId, v.VersionNumber, v.EffectiveFrom, v.EffectiveTo, v.Status, v.CreatedAt, v.UpdatedAt);

        public static VersionDetail ToDetail(StudyPlanVersion v)
            => new(ToSummary(v),
                [.. v.Courses.OrderBy(c => c.CourseVersionId).Select(c => new PlanCourseSummary(c.Id, c.CourseVersionId, c.AcademicLevelId, c.RecommendedTermId, c.RequirementType, c.RequiredCredits))],
                [.. v.Requirements.OrderBy(r => r.CourseVersionId).Select(r => new CourseRequirementSummary(r.Id, r.CourseVersionId, r.RequirementType))]);
    }
}
