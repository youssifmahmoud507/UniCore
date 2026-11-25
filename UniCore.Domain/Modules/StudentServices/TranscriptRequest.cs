using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.StudentServices
{
    // A student asks for an official transcript. When the workflow approves it, the official snapshot is issued
    // (ITranscriptSnapshotService) and its id is kept here. One open request per student.
    public sealed class TranscriptRequest : ServiceRequest
    {
        private TranscriptRequest()
        {
        }

        public string Purpose { get; private set; } = string.Empty;
        public Guid? ChargeId { get; private set; }
        public Guid? TranscriptSnapshotId { get; private set; }

        public bool IsIssued => TranscriptSnapshotId is not null;

        public static Result<TranscriptRequest> Create(Guid studentId, Guid requestedByUserId, string? purpose, DateTimeOffset now)
        {
            var text = RequiredText(purpose, ShortTextMaxLength);
            if (!text.TryGetValue(out var validPurpose))
            {
                return Result<TranscriptRequest>.Fail(text.Errors);
            }

            var request = new TranscriptRequest { Purpose = validPurpose };
            var started = request.Initialize(studentId, requestedByUserId, now);

            return started.IsFailure ? Result<TranscriptRequest>.Fail(started.Errors) : request;
        }

        public void RecordCharge(Guid chargeId) => ChargeId = chargeId;

        // The same snapshot twice is the same outcome; a different one is refused.
        public Result RecordIssued(Guid snapshotId, DateTimeOffset now)
        {
            if (Status != ServiceRequestStatus.Approved || snapshotId == Guid.Empty)
            {
                return Result.Fail(ServiceRequestErrors.InvalidState);
            }

            if (TranscriptSnapshotId is { } existing)
            {
                return existing == snapshotId ? Result.Ok() : Result.Fail(ServiceRequestErrors.InvalidState);
            }

            TranscriptSnapshotId = snapshotId;

            return Result.Ok();
        }
    }
}
