using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Notifications;

namespace UniCore.Application.Modules.Notifications.Specifications
{
    // The notifications that show in the user's in-app list, newest first.
    public sealed class MyNotificationsSpecification : Specification<Notification>
    {
        public MyNotificationsSpecification(PaginationQuery query, Guid userId, bool unreadOnly)
        {
            SetCriteria(n => n.UserId == userId && n.Deliveries.Any(d => d.Channel == NotificationChannel.InApp) && (!unreadOnly || n.ReadAt == null));

            ApplyOrderByDescending(n => n.CreatedAt);
            ApplyPaging(query);
        }
    }
}
