using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Courses.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Courses;

namespace UniCore.Application.Modules.Courses
{
    // Implements the read contract for the other modules. No permission checks (the callers are services, not users).
    public sealed class CourseCatalogReadService(IAggregateRepository<Course> courses, IAggregateRepository<CourseVersion> versions) : ICourseCatalogReadService
    {
        private readonly IAggregateRepository<Course> _courses = courses;
        private readonly IAggregateRepository<CourseVersion> _versions = versions;

        private static CourseVersionInfo ToInfo(CourseVersion v, Course c)
            => new(v.Id, c.Id, c.Code, c.DepartmentId, v.VersionNumber, v.Title, v.Credits, v.LectureHours, v.PracticalHours, v.Status, v.EffectiveFrom, v.EffectiveTo);

        public async Task<Result<CourseInfo>> GetCourseAsync(Guid courseId, CancellationToken cancellationToken = default)
        {
            var course = await _courses.GetByIdAsync(courseId, cancellationToken);

            return course is null ? CoursesErrors.CourseNotFound : new CourseInfo(course.Id, course.DepartmentId, course.Code, course.Title, course.CourseType, course.IsActive);
        }

        public async Task<Result<CourseVersionInfo>> GetVersionAsync(Guid courseVersionId, CancellationToken cancellationToken = default)
        {
            var version = await _versions.GetByIdAsync(courseVersionId, cancellationToken);
            if (version is null)
            {
                return CoursesErrors.VersionNotFound;
            }

            var course = await _courses.GetByIdAsync(version.CourseId, cancellationToken);

            return course is null ? CoursesErrors.CourseNotFound : ToInfo(version, course);
        }

        public async Task<Result<CourseVersionInfo>> GetEffectiveVersionAsync(Guid courseId, DateOnly date, CancellationToken cancellationToken = default)
        {
            var course = await _courses.GetByIdAsync(courseId, cancellationToken);
            if (course is null)
            {
                return CoursesErrors.CourseNotFound;
            }

            var version = await _versions.FirstOrDefaultAsync(new CourseEffectiveVersionSpecification(courseId, date), cancellationToken);

            return version is null ? CoursesErrors.VersionNotFound : ToInfo(version, course);
        }
    }
}
