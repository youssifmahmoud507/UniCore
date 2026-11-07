using UniCore.Application.Common.Repositories;
using UniCore.Domain.Modules.Offerings;

namespace UniCore.Application.Modules.Offerings
{
    // The enrolled count of a section is the seat counter kept by Registration on the section itself.
    public sealed class SectionSeatCounter(IAggregateRepository<CourseSection> sections) : ISectionEnrollmentCounter
    {
        private readonly IAggregateRepository<CourseSection> _sections = sections;

        public async Task<int> CountEnrolledAsync(Guid courseSectionId, CancellationToken cancellationToken = default)
            => (await _sections.GetByIdAsync(courseSectionId, cancellationToken))?.EnrolledCount ?? 0;
    }
}
