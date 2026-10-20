using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Notifications;

namespace UniCore.Application.Modules.Notifications.Specifications
{
    // A notification of this user only: someone else's looks like a missing one.
    public sealed class NotificationByIdSpecification : Specification<Notification>
    {
        public NotificationByIdSpecification(Guid id, Guid userId, bool tracking = false)
        {
            SetCriteria(n => n.Id == id && n.UserId == userId);

            if (tracking)
            {
                UseTracking();
            }
        }
    }
}
