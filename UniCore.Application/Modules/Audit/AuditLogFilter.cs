using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Application.Modules.Audit
{
    // Every field is optional; the filters are combined with AND.
    public sealed record AuditLogFilter(string? Action = null, Guid? ActorUserId = null, string? EntityName = null, string? EntityId = null, DateTimeOffset? From = null, DateTimeOffset? To = null);



}
