using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Notifications;

namespace UniCore.Application.Modules.Notifications.Specifications
{
    public sealed class PreferencesOfUserSpecification : Specification<NotificationPreference>
    {
        public PreferencesOfUserSpecification(Guid userId)
        {
            SetCriteria(p => p.UserId == userId);
        }
    }
}
