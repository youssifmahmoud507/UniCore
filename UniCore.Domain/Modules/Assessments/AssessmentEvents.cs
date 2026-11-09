using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Assessments
{
    // Raised when the grading scheme of a section is published (the Week 14 grading reads its components).
    // Consumers must be idempotent, keyed by EventId.
    public sealed record AssessmentPublished(Guid EventId, DateTimeOffset OccurredAt, Guid AssessmentId, Guid CourseSectionId, int ComponentCount) : IDomainEvent;

    // Raised when the grading scheme is closed (no more scores accepted against it).
    public sealed record AssessmentClosed(Guid EventId, DateTimeOffset OccurredAt, Guid AssessmentId, Guid CourseSectionId) : IDomainEvent;

    // Raised when a published scheme is cancelled.
    public sealed record AssessmentCancelled(Guid EventId, DateTimeOffset OccurredAt, Guid AssessmentId, Guid CourseSectionId) : IDomainEvent;
}
