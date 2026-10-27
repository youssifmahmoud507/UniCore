using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Offerings.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Offerings;

namespace UniCore.Application.Modules.Offerings
{
    // Implements the read contract for the other modules. No permission checks (the callers are services, not users).
    public sealed class OfferingReadService(
        IAggregateRepository<CourseOffering> offerings, IAggregateRepository<CourseSection> sections,
        IAggregateRepository<InstructorAssignment> assignments, ICourseCatalogReadService catalog) : IOfferingReadService
    {
        private readonly IAggregateRepository<CourseOffering> _offerings = offerings;
        private readonly IAggregateRepository<CourseSection> _sections = sections;
        private readonly IAggregateRepository<InstructorAssignment> _assignments = assignments;
        private readonly ICourseCatalogReadService _catalog = catalog;

        public async Task<Result<OfferingInfo>> GetOfferingAsync(Guid offeringId, CancellationToken cancellationToken = default)
        {
            var offering = await _offerings.GetByIdAsync(offeringId, cancellationToken);
            if (offering is null)
            {
                return OfferingsErrors.OfferingNotFound;
            }

            var version = await _catalog.GetVersionAsync(offering.CourseVersionId, cancellationToken);
            if (!version.TryGetValue(out var info))
            {
                return Result<OfferingInfo>.Fail(version.Errors);
            }

            return new OfferingInfo(offering.Id, offering.CourseVersionId, info.CourseId, offering.AcademicSemesterId, offering.Status, offering.MaxSections, offering.AcceptsEnrollment);
        }

        public async Task<Result<SectionInfo>> GetSectionAsync(Guid sectionId, CancellationToken cancellationToken = default)
        {
            var section = await _sections.GetByIdAsync(sectionId, cancellationToken);
            if (section is null)
            {
                return OfferingsErrors.SectionNotFound;
            }

            var offering = await _offerings.GetByIdAsync(section.CourseOfferingId, cancellationToken);
            if (offering is null)
            {
                return OfferingsErrors.OfferingNotFound;
            }

            var version = await _catalog.GetVersionAsync(offering.CourseVersionId, cancellationToken);
            if (!version.TryGetValue(out var info))
            {
                return Result<SectionInfo>.Fail(version.Errors);
            }

            return new SectionInfo(
                section.Id, offering.Id, offering.CourseVersionId, info.CourseId, offering.AcademicSemesterId, section.SectionCode, section.Capacity,
                section.Status, offering.Status, info.Credits, offering.AcceptsEnrollment && section.IsOpen);
        }

        public async Task<Result<IReadOnlyList<SectionInstructorInfo>>> GetActiveInstructorsAsync(Guid sectionId, CancellationToken cancellationToken = default)
        {
            if (await _sections.GetByIdAsync(sectionId, cancellationToken) is null)
            {
                return OfferingsErrors.SectionNotFound;
            }

            var items = await _assignments.ListAsync(new AssignmentsOfSectionSpecification(sectionId, activeOnly: true), cancellationToken);

            return Result<IReadOnlyList<SectionInstructorInfo>>.Ok([.. items.Select(a => new SectionInstructorInfo(a.InstructorId, a.Role, a.AssignedAt))]);
        }
    }
}
