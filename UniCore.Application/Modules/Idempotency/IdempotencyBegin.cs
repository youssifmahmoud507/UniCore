namespace UniCore.Application.Modules.Idempotency
{
    public sealed record IdempotencyBegin(IdempotencyOutcome Outcome, Guid RecordId, StoredResponse? Response)
    {
        public static IdempotencyBegin Started(Guid recordId) => new(IdempotencyOutcome.Started, recordId, null);

        public static IdempotencyBegin Replay(Guid recordId, StoredResponse response) => new(IdempotencyOutcome.Replay, recordId, response);

        public static IdempotencyBegin InProgress(Guid recordId) => new(IdempotencyOutcome.InProgress, recordId, null);

        public static IdempotencyBegin Mismatch(Guid recordId) => new(IdempotencyOutcome.Mismatch, recordId, null);
    }

}
