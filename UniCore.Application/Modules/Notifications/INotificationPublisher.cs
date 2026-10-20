using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Notifications
{
    // The contract the producing modules (Developer 1 too) call. The user's preferences decide the channels.
    public interface INotificationPublisher
    {
        Task<Result<NotificationPublishResult>> PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default);
    }


}
