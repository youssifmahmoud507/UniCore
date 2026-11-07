using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Scheduling
{
    // Raised when a section gets a meeting (day, time, room). Consumers must be idempotent, keyed by EventId.
    public sealed record SectionScheduled(Guid EventId, DateTimeOffset OccurredAt, Guid AssignmentId, Guid CourseSectionId, Guid RoomId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime) : IDomainEvent;
}
