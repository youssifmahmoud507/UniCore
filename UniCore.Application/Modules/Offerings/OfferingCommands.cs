using UniCore.Domain.Modules.Offerings;

namespace UniCore.Application.Modules.Offerings
{
    // Nullable fields: a missing value is reported by the domain as a validation error instead of becoming a default.

    // MaxSections null = no limit.
    public sealed record CreateOfferingCommand(Guid CourseVersionId, Guid AcademicSemesterId, int? MaxSections);
    public sealed record UpdateOfferingCommand(int? MaxSections);

    public sealed record CreateSectionCommand(string? SectionCode, int? Capacity);
    public sealed record UpdateSectionCommand(int? Capacity);

    public sealed record AssignInstructorCommand(Guid InstructorId, AssignmentRole? Role);
}
