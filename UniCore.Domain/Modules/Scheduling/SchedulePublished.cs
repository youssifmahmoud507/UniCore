using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Scheduling
{
    // Raised when a schedule is published (notifications to students and instructors, Developer 2).
    public sealed record SchedulePublished(Guid EventId, DateTimeOffset OccurredAt, Guid ScheduleId, Guid AcademicSemesterId) : IDomainEvent;
}
