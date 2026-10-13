using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Audit
{
    // One audit record. Append-only: nothing on this class changes it after creation.
    // Never put passwords, tokens, OTPs or hashes in it.
    public sealed class AuditLog : BaseEntity
    {
        public const int MaxActionLength = 100;
        public const int MaxEntityNameLength = 100;
        public const int MaxEntityIdLength = 100;
        public const int MaxIpLength = 45;
        public const int MaxUserAgentLength = 256;
        public const int MaxCorrelationIdLength = 100;
        public const int MaxValuesLength = 4000;

        private AuditLog()
        {
        }

        public string Action { get; private set; } = string.Empty;
        public Guid? ActorUserId { get; private set; }
        public string? EntityName { get; private set; }
        public string? EntityId { get; private set; }
        public string? IpAddress { get; private set; }
        public string? UserAgent { get; private set; }
        public string? OldValues { get; private set; }
        public string? NewValues { get; private set; }
        public string? CorrelationId { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }

        // Only the action is validated. Every other field is cleaned and capped, never rejected,
        // because losing an audit event is worse than shortening a long user agent.
        public static Result<AuditLog> Create(string? action, Guid? actorUserId, string? entityName, string? entityId, string? ipAddress,string? userAgent, string? oldValues, string? newValues, string? correlationId, DateTimeOffset now)
        {
            var cleanAction = Clean(action, int.MaxValue);

            if (cleanAction is null || cleanAction.Length > MaxActionLength)
            {
                return AuditErrors.ActionInvalid;
            }

            return new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = cleanAction,
                ActorUserId = actorUserId,
                EntityName = Clean(entityName, MaxEntityNameLength),
                EntityId = Clean(entityId, MaxEntityIdLength),
                IpAddress = Clean(ipAddress, MaxIpLength),
                UserAgent = Clean(userAgent, MaxUserAgentLength),
                OldValues = Clean(oldValues, MaxValuesLength),
                NewValues = Clean(newValues, MaxValuesLength),
                CorrelationId = Clean(correlationId, MaxCorrelationIdLength),
                CreatedAt = now
            };
        }

        // Strips control characters (log injection) and caps the length. Blank becomes null.
        private static string? Clean(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var cleaned = new string([.. value.Where(c => !char.IsControl(c))]).Trim();

            if (cleaned.Length == 0)
            {
                return null;
            }

            return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
        }
    }



}
