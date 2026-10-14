using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Audit
{
    public static class AuditErrors
    {
        public static readonly Error ActionInvalid = Error.Validation("AUDIT_ACTION_INVALID", "The audit action is required and must be at most 100 characters.");
        public static readonly Error FilterInvalid = Error.Validation("AUDIT_FILTER_INVALID", "The audit filter is not valid: the start must not be after the end, and text filters must not be longer than their column.");
        public static readonly Error LogNotFound = Error.NotFound("AUDIT_LOG_NOT_FOUND", "The audit log entry was not found.");
        public static readonly Error StorageUnavailable = Error.External("AUDIT_STORAGE_UNAVAILABLE", "The audit log could not be read right now.");
    }


}
