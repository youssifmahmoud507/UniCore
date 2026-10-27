using UniCore.Domain.Modules.Offerings;

namespace UniCore.Application.Modules.Offerings
{
    public sealed record OfferingSummary(Guid Id, Guid CourseVersionId, Guid AcademicSemesterId, CourseOfferingStatus Status, int? MaxSections, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    public sealed record SectionSummary(Guid Id, Guid CourseOfferingId, string SectionCode, int Capacity, CourseSectionStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    public sealed record AssignmentSummary(Guid Id, Guid CourseSectionId, Guid InstructorId, AssignmentRole Role, DateTimeOffset AssignedAt, DateTimeOffset? EndedAt);
}
