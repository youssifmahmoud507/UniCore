using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Extensions;
using UniCore.Api.Security;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Api.Controllers
{
    // Read-only. No POST, PUT or DELETE: the audit trail is append-only.
    [ApiController]
    [Route("api/v1/audit-logs")]
    [Authorize]
    public sealed class AuditLogsController : ControllerBase
    {
        [HttpGet]
        [HasPermission(Permissions.Audit.Read)]
        public async Task<IActionResult> List([FromServices] AuditLogQueryHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] string? action = null, [FromQuery] Guid? actorUserId = null, [FromQuery] string? entityName = null, [FromQuery] string? entityId = null, [FromQuery] DateTimeOffset? from = null, [FromQuery] DateTimeOffset? to = null)=> (await handler.ListAsync(new AuditLogFilter(action, actorUserId, entityName, entityId, from, to), new PaginationQuery(page, pageSize), cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("{id:guid}")]
        [HasPermission(Permissions.Audit.Read)]
        public async Task<IActionResult> Get(Guid id, [FromServices] AuditLogQueryHandler handler, CancellationToken cancellationToken)=> (await handler.GetAsync(id, cancellationToken)).ToActionResult(HttpContext);
    }
}
