using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Notifications
{
    // A user's choice of channels for one notification type. No row means both channels are on.
    public sealed class NotificationPreference : AggregateRoot
    {
        private NotificationPreference()
        {
        }

        public Guid UserId { get; private set; }
        public string NotificationType { get; private set; } = string.Empty;
        public bool InAppEnabled { get; private set; }
        public bool EmailEnabled { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }

        public static Result<NotificationPreference> Create(Guid userId, string? notificationType, bool inAppEnabled, bool emailEnabled, DateTimeOffset now)
        {
            var type = Notification.NormalizeType(notificationType);

            if (userId == Guid.Empty || type is null)
            {
                return NotificationErrors.PreferenceInvalid;
            }

            return new NotificationPreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                NotificationType = type,
                InAppEnabled = inAppEnabled,
                EmailEnabled = emailEnabled,
                UpdatedAt = now
            };
        }

        public void Update(bool inAppEnabled, bool emailEnabled, DateTimeOffset now)
        {
            InAppEnabled = inAppEnabled;
            EmailEnabled = emailEnabled;
            UpdatedAt = now;
        }
    }




}
