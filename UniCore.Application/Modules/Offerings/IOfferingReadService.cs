using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Offerings;

namespace UniCore.Application.Modules.Offerings
{
    // Read contract of the Offerings module for the other modules (registration, scheduling, attendance, exams,
    // assessment, grading). Errors: OFFERING_NOT_FOUND, SECTION_NOT_FOUND, COURSE_VERSION_NOT_FOUND.
    public interface IOfferingReadService
    {
        Task<Result<OfferingInfo>> GetOfferingAsync(Guid offeringId, CancellationToken cancellationToken = default);

        Task<Result<SectionInfo>> GetSectionAsync(Guid sectionId, CancellationToken cancellationToken = default);

        // Instructors whose assignment has not ended.
        Task<Result<IReadOnlyList<SectionInstructorInfo>>> GetActiveInstructorsAsync(Guid sectionId, CancellationToken cancellationToken = default);
    }

    public sealed record OfferingInfo(Guid OfferingId, Guid CourseVersionId, Guid CourseId, Guid AcademicSemesterId, CourseOfferingStatus Status, int? MaxSections, bool AcceptsEnrollment);

    // Credits come from the offering's course version. IsOpenForEnrollment: offering Open and section Open.
    public sealed record SectionInfo(
        Guid SectionId, Guid OfferingId, Guid CourseVersionId, Guid CourseId, Guid AcademicSemesterId, string SectionCode, int Capacity,
        CourseSectionStatus SectionStatus, CourseOfferingStatus OfferingStatus, int Credits, bool IsOpenForEnrollment);

    public sealed record SectionInstructorInfo(Guid InstructorId, AssignmentRole Role, DateTimeOffset AssignedAt);
}
