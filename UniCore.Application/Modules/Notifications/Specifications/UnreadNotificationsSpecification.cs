using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Notifications;

namespace UniCore.Application.Modules.Notifications.Specifications
{
    // Unread ones, tracked: the handler marks them read.
    public sealed class UnreadNotificationsSpecification : Specification<Notification>
    {
        public UnreadNotificationsSpecification(PaginationQuery query, Guid userId)
        {
            SetCriteria(n => n.UserId == userId && n.ReadAt == null && n.Deliveries.Any(d => d.Channel == NotificationChannel.InApp));

            ApplyOrderBy(n => n.CreatedAt);
            ApplyPaging(query);
            UseTracking();
        }
    }
}
