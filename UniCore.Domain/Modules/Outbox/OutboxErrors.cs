using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Outbox
{
    public static class OutboxErrors
    {
        public static readonly Error TypeInvalid = Error.Validation("OUTBOX_TYPE_INVALID", "The outbox message type is required.");
        public static readonly Error PayloadInvalid = Error.Validation("OUTBOX_PAYLOAD_INVALID", "The outbox message payload is missing or cannot be read.");
        public static readonly Error MessageFinished = Error.Conflict("OUTBOX_MESSAGE_FINISHED", "The outbox message has already been processed or has failed for good.");
        public static readonly Error SerializationFailed = Error.Failure("OUTBOX_SERIALIZATION_FAILED", "A domain event could not be turned into an outbox message.");
        public static readonly Error HandlerNotFound = Error.NotFound("OUTBOX_HANDLER_NOT_FOUND", "No handler is registered for this outbox message type.");
        public static readonly Error HandlerFailed = Error.Failure("OUTBOX_HANDLER_FAILED", "The outbox message handler failed unexpectedly.");
        public static readonly Error StorageUnavailable = Error.External("OUTBOX_STORAGE_UNAVAILABLE", "The outbox could not be read or written right now.");
    }



}
