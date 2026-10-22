using UniCore.Domain.Modules.Courses;

namespace UniCore.Application.Modules.Courses
{
    // Nullable fields: a missing value is reported by the domain as a validation error instead of becoming a default.
    public sealed record CreateCourseCommand(Guid DepartmentId, string? Code, string? Title, string? Description, CourseType? CourseType);
    public sealed record UpdateCourseCommand(string? Title, string? Description, CourseType? CourseType);

    // SourceVersionId: start the draft as a copy of this version. Any field left null then keeps the source value,
    // so changing only the credits is a one-field request. Without a source every field is required.
    public sealed record CreateCourseVersionCommand(Guid? SourceVersionId, string? Title, string? Description, int? Credits, int? LectureHours, int? PracticalHours, DateOnly? EffectiveFrom);

    // Replaces all the editable fields of a draft.
    public sealed record UpdateCourseVersionCommand(string? Title, string? Description, int? Credits, int? LectureHours, int? PracticalHours, DateOnly? EffectiveFrom);

    public sealed record RetireCourseVersionCommand(DateOnly? EffectiveTo);
}
