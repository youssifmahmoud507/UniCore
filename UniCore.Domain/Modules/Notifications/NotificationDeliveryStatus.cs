namespace UniCore.Domain.Modules.Notifications
{
    // InApp: Sent once it is saved. Email: Queued once the outbox accepted it (the outbox delivers it and retries); Failed if it could not be queued.
    public enum NotificationDeliveryStatus
    {
        Pending = 0,
        Queued = 1,
        Sent = 2,
        Failed = 3
    }




}
