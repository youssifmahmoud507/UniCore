using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Notifications;

namespace UniCore.Application.Modules.Notifications.Specifications
{
    public sealed class NotificationBySourceSpecification : Specification<Notification>
    {
        public NotificationBySourceSpecification(Guid userId, Guid sourceEventId, string type)
        {
            SetCriteria(n => n.UserId == userId && n.SourceEventId == sourceEventId && n.Type == type);
        }
    }
}
