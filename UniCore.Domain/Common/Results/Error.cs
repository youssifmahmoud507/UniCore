namespace UniCore.Domain.Common.Results
{
    public sealed record Error(string Code, string Description, ErrorType ErrorType = ErrorType.Failure)
    {
        public static Error Failure(string code = "GENERAL_FAILURE", string description = "A general failure has occurred.") => new(code, description, ErrorType.Failure);
        public static Error Validation(string code = "GENERAL_VALIDATION", string description = "A validation error has occurred.") => new(code, description, ErrorType.Validation);
        public static Error NotFound(string code = "GENERAL_NOT_FOUND", string description = "The requested resource was not found.") => new(code, description, ErrorType.NotFound);
        public static Error Conflict(string code = "GENERAL_CONFLICT", string description = "A conflict has occurred.") => new(code, description, ErrorType.Conflict);
        public static Error Unauthorized(string code = "GENERAL_UNAUTHORIZED", string description = "Authentication is required or has failed.") => new(code, description, ErrorType.Unauthorized);
        public static Error Forbidden(string code = "GENERAL_FORBIDDEN", string description = "This operation is forbidden.") => new(code, description, ErrorType.Forbidden);
        public static Error InvalidCredentials(string code = "INVALID_CREDENTIALS", string description = "The provided credentials are invalid.") => new(code, description, ErrorType.InvalidCredentials);
        public static Error BusinessRule(string code = "GENERAL_BUSINESS_RULE", string description = "A business rule was violated.") => new(code, description, ErrorType.BusinessRule);
        public static Error Concurrency(string code = "CONCURRENCY_CONFLICT", string description = "The resource was modified by another operation.") => new(code, description, ErrorType.Concurrency);
        public static Error External(string code = "EXTERNAL_SERVICE_UNAVAILABLE", string description = "An external service is unavailable.") => new(code, description, ErrorType.External);
    }
}
