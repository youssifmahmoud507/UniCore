using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Attendance
{
    // Raised when a student's attendance in a section falls to a new (worse) level. Notifications listen to it.
    // Consumers must be idempotent, keyed by EventId. Percentage has 2 decimals.
    public sealed record AttendanceWarningIssued(Guid EventId, DateTimeOffset OccurredAt, Guid WarningId, Guid StudentId, Guid CourseSectionId, decimal Percentage, WarningLevel Level, bool IsExamBlocking) : IDomainEvent;
}
