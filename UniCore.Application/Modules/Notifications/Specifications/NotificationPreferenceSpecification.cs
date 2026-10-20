using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Notifications;

namespace UniCore.Application.Modules.Notifications.Specifications
{
    public sealed class NotificationPreferenceSpecification : Specification<NotificationPreference>
    {
        public NotificationPreferenceSpecification(Guid userId, string type, bool tracking = false)
        {
            SetCriteria(p => p.UserId == userId && p.NotificationType == type);

            if (tracking)
            {
                UseTracking();
            }
        }
    }
}
