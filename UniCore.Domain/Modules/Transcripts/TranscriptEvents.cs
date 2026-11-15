using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Transcripts
{
    // Raised when an official transcript is issued. Document integration (Developer 2) renders and stores the file; Notification tells the student.
    // Consumers must be idempotent, keyed by EventId.
    public sealed record TranscriptIssued(Guid EventId, DateTimeOffset OccurredAt, Guid SnapshotId, Guid StudentId, string VerificationCode, decimal CumulativeGpa) : IDomainEvent;
}
