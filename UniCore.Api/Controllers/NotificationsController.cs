using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniCore.Api.Contracts.Notifications;
using UniCore.Api.Extensions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Notifications;

namespace UniCore.Api.Controllers
{
    // The logged-in user's own notifications.
    [ApiController]
    [Route("api/v1/me/notifications")]
    [Authorize]
    public sealed class NotificationsController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List([FromServices] MyNotificationsHandler handler, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationQuery.DefaultPageSize, [FromQuery] bool unreadOnly = false)
            => (await handler.ListAsync(new PaginationQuery(page, pageSize), unreadOnly, cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("unread-count")]
        public async Task<IActionResult> UnreadCount([FromServices] MyNotificationsHandler handler, CancellationToken cancellationToken)
            => (await handler.UnreadCountAsync(cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("{id:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid id, [FromServices] MyNotificationsHandler handler, CancellationToken cancellationToken)
            => (await handler.MarkReadAsync(id, cancellationToken)).ToActionResult(HttpContext);

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllRead([FromServices] MyNotificationsHandler handler, CancellationToken cancellationToken)
            => (await handler.MarkAllReadAsync(cancellationToken)).ToActionResult(HttpContext);

        [HttpGet("preferences")]
        public async Task<IActionResult> Preferences([FromServices] MyNotificationsHandler handler, CancellationToken cancellationToken)
            => (await handler.GetPreferencesAsync(cancellationToken)).ToActionResult(HttpContext);

        [HttpPut("preferences/{type}")]
        public async Task<IActionResult> SetPreference(string type, [FromBody] SetNotificationPreferenceRequest request, [FromServices] MyNotificationsHandler handler, CancellationToken cancellationToken)
            => (await handler.SetPreferenceAsync(type, request.InAppEnabled, request.EmailEnabled, cancellationToken)).ToActionResult(HttpContext);
    }

}
