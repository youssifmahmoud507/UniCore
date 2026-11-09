using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Examination
{
    // Raised when an exam is published (students and invigilators are told). Consumers must be idempotent, keyed by EventId.
    public sealed record ExamPublished(Guid EventId, DateTimeOffset OccurredAt, Guid ExamId, Guid CourseSectionId, ExamType ExamType, DateOnly ExamDate, TimeOnly StartTime, TimeOnly EndTime) : IDomainEvent;

    // Raised when a published exam is cancelled.
    public sealed record ExamCancelled(Guid EventId, DateTimeOffset OccurredAt, Guid ExamId, Guid CourseSectionId, ExamType ExamType) : IDomainEvent;
}
