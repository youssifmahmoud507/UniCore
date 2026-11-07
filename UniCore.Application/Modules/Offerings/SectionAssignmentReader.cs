using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Offerings.Specifications;
using UniCore.Application.Modules.People;
using UniCore.Domain.Modules.Offerings;

namespace UniCore.Application.Modules.Offerings
{
    // The sections an instructor teaches right now (active assignments, any role). People's scope data and the
    // Scheduling instructor-conflict rule read it. Teaching assistants have no assignment rows yet, so they get none.
    public sealed class SectionAssignmentReader(IAggregateRepository<InstructorAssignment> assignments) : ISectionAssignmentReader
    {
        private readonly IAggregateRepository<InstructorAssignment> _assignments = assignments;

        public async Task<IReadOnlyList<Guid>> GetAssignedSectionIdsAsync(Guid? instructorId, Guid? teachingAssistantId, CancellationToken cancellationToken = default)
        {
            if (instructorId is not { } id)
            {
                return [];
            }

            var list = await _assignments.ListAsync(new ActiveAssignmentsOfInstructorSpecification(id), cancellationToken);

            return [.. list.Select(a => a.CourseSectionId).Distinct()];
        }
    }
}
