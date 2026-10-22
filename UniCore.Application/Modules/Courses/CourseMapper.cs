using UniCore.Domain.Modules.Courses;

namespace UniCore.Application.Modules.Courses
{
    // Entity -> DTO. The entities are never returned to the API directly.
    public static class CourseMapper
    {
        public static CourseSummary ToSummary(Course c)
            => new(c.Id, c.DepartmentId, c.Code, c.Title, c.Description, c.CourseType, c.IsActive, c.CreatedAt, c.UpdatedAt);

        public static CourseVersionSummary ToSummary(CourseVersion v)
            => new(v.Id, v.CourseId, v.VersionNumber, v.Title, v.Description, v.Credits, v.LectureHours, v.PracticalHours, v.EffectiveFrom, v.EffectiveTo, v.Status, v.CreatedAt, v.UpdatedAt);
    }
}
