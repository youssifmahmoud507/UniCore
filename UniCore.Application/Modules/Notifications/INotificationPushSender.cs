namespace UniCore.Application.Modules.Notifications
{
    // Pushes a new notification to the person's open browser tabs (SignalR). Best effort: it never fails the caller,
    // because the notification is already saved and shows in the list anyway.
    public interface INotificationPushSender
    {
        Task PushAsync(Guid userId, NotificationDto notification, int unreadCount, CancellationToken cancellationToken = default);
    }

}
