using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Idempotency
{
    public static class IdempotencyErrors
    {
        public static readonly Error KeyRequired = Error.Validation("IDEMPOTENCY_KEY_REQUIRED", "The Idempotency-Key header is required for this request.");
        public static readonly Error KeyInvalid = Error.Validation("IDEMPOTENCY_KEY_INVALID", "The Idempotency-Key header must be 8 to 100 visible characters, no spaces.");
        public static readonly Error KeyReused = Error.Conflict("IDEMPOTENCY_KEY_REUSED", "This Idempotency-Key was already used for a different request.");
        public static readonly Error InProgress = Error.Conflict("IDEMPOTENCY_REQUEST_IN_PROGRESS", "A request with this Idempotency-Key is still being processed. Try again shortly.");
        public static readonly Error RecordDataInvalid = Error.Validation("IDEMPOTENCY_RECORD_INVALID", "The idempotency record data is invalid.");
        public static readonly Error RecordNotFound = Error.NotFound("IDEMPOTENCY_RECORD_NOT_FOUND", "The idempotency record was not found.");
        public static readonly Error RecordAlreadyCompleted = Error.Conflict("IDEMPOTENCY_RECORD_COMPLETED", "The idempotency record is already completed.");
        public static readonly Error StorageUnavailable = Error.External("IDEMPOTENCY_STORAGE_UNAVAILABLE", "Idempotency records could not be read or written right now.");
    }



}
