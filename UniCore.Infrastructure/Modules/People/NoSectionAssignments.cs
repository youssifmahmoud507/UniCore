using UniCore.Application.Modules.People;

namespace UniCore.Infrastructure.Modules.People
{
    // Placeholder until course sections and instructor assignments exist (Week 8).
    // Replace this registration with the real reader then; the scope data keeps the same shape.
    public sealed class NoSectionAssignments : ISectionAssignmentReader
    {
        public Task<IReadOnlyList<Guid>> GetAssignedSectionIdsAsync(Guid? instructorId, Guid? teachingAssistantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
