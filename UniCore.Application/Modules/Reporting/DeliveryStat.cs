using UniCore.Domain.Modules.Notifications;

namespace UniCore.Application.Modules.Reporting
{
    public sealed record DeliveryStat(NotificationChannel Channel, NotificationDeliveryStatus Status, int Count);




}
