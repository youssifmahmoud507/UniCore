namespace UniCore.Domain.Common.Results
{
    public static class CommonErrors
    {
        public static readonly Error ConcurrencyConflict = Error.Concurrency();
        public static readonly Error DuplicateResource = Error.Conflict("DUPLICATE_RESOURCE", "A resource with the same unique value already exists.");
        public static readonly Error ExternalServiceUnavailable = Error.External();
        public static readonly Error DatabaseTimeout = Error.External("DATABASE_TIMEOUT", "The database did not respond in time.");
        public static readonly Error DatabaseUpdateFailed = Error.Failure("DATABASE_UPDATE_FAILED", "The changes could not be saved.");
        public static readonly Error OperationCancelled = Error.Failure("OPERATION_CANCELLED", "The operation was cancelled.");
    }
}