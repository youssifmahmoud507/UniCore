using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Notifications
{
    // How one channel got on with one notification. Only Notification creates deliveries.
    public sealed class NotificationDelivery : BaseEntity
    {
        public const int MaxErrorLength = 200;

        private NotificationDelivery()
        {
        }

        public Guid NotificationId { get; private set; }
        public NotificationChannel Channel { get; private set; }
        public NotificationDeliveryStatus Status { get; private set; }
        public int AttemptCount { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? SentAt { get; private set; }
        public DateTimeOffset? FailedAt { get; private set; }
        public string? ErrorMessage { get; private set; }

        internal static NotificationDelivery Create(Guid notificationId, NotificationChannel channel, NotificationDeliveryStatus status, string? error, DateTimeOffset now)
        {
            var message = error?.Trim();

            if (message is { Length: > MaxErrorLength })
            {
                message = message[..MaxErrorLength];
            }

            return new NotificationDelivery
            {
                Id = Guid.NewGuid(),
                NotificationId = notificationId,
                Channel = channel,
                Status = status,
                AttemptCount = 1,
                CreatedAt = now,
                SentAt = status == NotificationDeliveryStatus.Sent ? now : null,
                FailedAt = status == NotificationDeliveryStatus.Failed ? now : null,
                ErrorMessage = status == NotificationDeliveryStatus.Failed ? message : null
            };
        }
    }




}
