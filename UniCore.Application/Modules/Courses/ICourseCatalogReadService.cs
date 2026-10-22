using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Courses;

namespace UniCore.Application.Modules.Courses
{
    // Read contract of the Courses module for the other modules (prerequisites, offerings, registration, grading,
    // transcripts). They match students and attempts by Course, and use a CourseVersion for credits and hours.
    // Errors: COURSE_NOT_FOUND, COURSE_VERSION_NOT_FOUND.
    public interface ICourseCatalogReadService
    {
        Task<Result<CourseInfo>> GetCourseAsync(Guid courseId, CancellationToken cancellationToken = default);

        Task<Result<CourseVersionInfo>> GetVersionAsync(Guid courseVersionId, CancellationToken cancellationToken = default);

        // The version whose window covers the date (an Active or Retired one, never a Draft).
        // COURSE_VERSION_NOT_FOUND when no version covers it.
        Task<Result<CourseVersionInfo>> GetEffectiveVersionAsync(Guid courseId, DateOnly date, CancellationToken cancellationToken = default);
    }

    public sealed record CourseInfo(Guid CourseId, Guid DepartmentId, string Code, string Title, CourseType CourseType, bool IsActive);

    public sealed record CourseVersionInfo(
        Guid CourseVersionId, Guid CourseId, string CourseCode, Guid DepartmentId, int VersionNumber, string Title,
        int Credits, int LectureHours, int PracticalHours, CourseVersionStatus Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
}
