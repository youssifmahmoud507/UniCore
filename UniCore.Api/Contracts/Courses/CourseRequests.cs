using UniCore.Domain.Modules.Courses;

namespace UniCore.Api.Contracts.Courses
{
    // Ids, numbers and enums are nullable so a missing value is reported as a validation error instead of silently becoming a default.
    public sealed record CreateCourseRequest(Guid? DepartmentId, string? Code, string? Title, string? Description, CourseType? CourseType);
    public sealed record UpdateCourseRequest(string? Title, string? Description, CourseType? CourseType);

    // SourceVersionId: copy this version; fields left out keep the source values.
    public sealed record CreateCourseVersionRequest(Guid? SourceVersionId, string? Title, string? Description, int? Credits, int? LectureHours, int? PracticalHours, DateOnly? EffectiveFrom);
    public sealed record UpdateCourseVersionRequest(string? Title, string? Description, int? Credits, int? LectureHours, int? PracticalHours, DateOnly? EffectiveFrom);
    public sealed record RetireCourseVersionRequest(DateOnly? EffectiveTo);
}
