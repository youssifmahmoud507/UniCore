using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Audit
{
    public static class AuditErrors
    {
        public static readonly Error ActionInvalid = Error.Validation("AUDIT_ACTION_INVALID", "The audit action is required and must be at most 100 characters.");
    }
}
