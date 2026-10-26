using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace UniCore.Infrastructure.Modules.Notifications
{
    // The browser connects here (with its JWT) to be told about new notifications right away.
    // The hub only PUSHES. Everything a person can ask for (the list, reading, the counter) is in the REST API,
    // and the database stays the source of truth.
    [Authorize]
    public sealed class NotificationHub : Hub
    {
        public const string Path = "/hubs/notifications";

        // One group per person: all their open tabs get the same push.
        public static string GroupOf(Guid userId) => $"user:{userId:D}";

        public override async Task OnConnectedAsync()
        {
            var raw = Context.User?.FindFirst("sub")?.Value ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(raw, out var userId))
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(userId));
            await base.OnConnectedAsync();
        }
    }

}
