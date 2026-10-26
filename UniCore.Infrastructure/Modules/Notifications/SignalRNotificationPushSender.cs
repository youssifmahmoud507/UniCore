using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using UniCore.Application.Modules.Notifications;

namespace UniCore.Infrastructure.Modules.Notifications
{
    public sealed class SignalRNotificationPushSender(IHubContext<NotificationHub> hub, ILogger<SignalRNotificationPushSender> logger) : INotificationPushSender
    {
        private readonly IHubContext<NotificationHub> _hub = hub;
        private readonly ILogger<SignalRNotificationPushSender> _logger = logger;

        public async Task PushAsync(Guid userId, NotificationDto notification, int unreadCount, CancellationToken cancellationToken = default)
        {
            try
            {
                await _hub.Clients.Group(NotificationHub.GroupOf(userId)).SendAsync("notification", new NotificationPush(notification, unreadCount), cancellationToken);
            }
            catch (Exception ex)
            {
                // The notification is saved. A failed push (a closed connection, a hub error) loses nothing.
                _logger.LogWarning("A notification push failed ({ExceptionType}).", ex.GetType().Name);
            }
        }
    }

}
