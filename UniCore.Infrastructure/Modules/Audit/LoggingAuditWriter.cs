using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Abstractions;

namespace UniCore.Infrastructure.Modules.Audit
{
    // Temporary writer: audit events go to the structured log (with the CorrelationId).
    // Replaced by the database AuditLog writer in the Audit week; the interface does not change.
    public sealed class LoggingAuditWriter(ILogger<LoggingAuditWriter> logger) : IAuditWriter
    {
        private const int MaxLength = 200;

        private readonly ILogger<LoggingAuditWriter> _logger = logger;

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "AUDIT {Action} actor={ActorUserId} entity={EntityName}/{EntityId} ip={IpAddress} userAgent={UserAgent}",
                entry.Action,
                entry.ActorUserId,
                entry.EntityName,
                entry.EntityId,
                Clean(entry.IpAddress),
                Clean(entry.UserAgent));

            return Task.CompletedTask;
        }

        // Client supplied values: strip control characters (log injection) and cap the length.
        private static string? Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var cleaned = new string(value.Where(c => !char.IsControl(c)).ToArray());

            return cleaned.Length <= MaxLength ? cleaned : cleaned[..MaxLength];
        }
    }
}
