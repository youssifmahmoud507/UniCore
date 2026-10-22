using UniCore.Domain.Modules.Courses;

namespace UniCore.Application.Modules.Courses
{
    public sealed record CourseSummary(Guid Id, Guid DepartmentId, string Code, string Title, string? Description, CourseType CourseType, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    public sealed record CourseVersionSummary(
        Guid Id, Guid CourseId, int VersionNumber, string Title, string? Description, int Credits, int LectureHours, int PracticalHours,
        DateOnly EffectiveFrom, DateOnly? EffectiveTo, CourseVersionStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
}
