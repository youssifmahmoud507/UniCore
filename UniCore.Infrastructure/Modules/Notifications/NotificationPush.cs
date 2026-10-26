using UniCore.Application.Modules.Notifications;

namespace UniCore.Infrastructure.Modules.Notifications
{
    // What the browser receives under the name "notification".
    public sealed record NotificationPush(NotificationDto Notification, int UnreadCount);

}
