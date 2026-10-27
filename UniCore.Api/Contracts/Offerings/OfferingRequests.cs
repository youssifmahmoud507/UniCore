using UniCore.Domain.Modules.Offerings;

namespace UniCore.Api.Contracts.Offerings
{
    // Ids, numbers and enums are nullable so a missing value is reported as a validation error instead of silently becoming a default.

    // MaxSections empty = no limit.
    public sealed record CreateOfferingRequest(Guid? CourseVersionId, Guid? AcademicSemesterId, int? MaxSections);
    public sealed record UpdateOfferingRequest(int? MaxSections);

    public sealed record CreateSectionRequest(string? SectionCode, int? Capacity);
    public sealed record UpdateSectionRequest(int? Capacity);

    public sealed record AssignInstructorRequest(Guid? InstructorId, AssignmentRole? Role);
}
