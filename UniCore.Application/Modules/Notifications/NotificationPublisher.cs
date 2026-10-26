using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Notifications.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Notifications;

namespace UniCore.Application.Modules.Notifications
{
    // The producers call this. It is safe to call twice for the same event (delivery is at least once).
    // Email goes through the outbox (retries there); the in-app copy is the notification row itself.
    public sealed class NotificationPublisher(IAggregateRepository<Notification> notifications, IAggregateRepository<NotificationPreference> preferences, IUserContactLookup contacts, IEmailQueue emailQueue, INotificationSettings settings, IUnitOfWork unitOfWork, INotificationPushSender push , IClock clock) : INotificationPublisher
    {
        private readonly IAggregateRepository<Notification> _notifications = notifications;
        private readonly IAggregateRepository<NotificationPreference> _preferences = preferences;
        private readonly IUserContactLookup _contacts = contacts;
        private readonly IEmailQueue _emailQueue = emailQueue;
        private readonly INotificationSettings _settings = settings;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly INotificationPushSender _push = push;
        private readonly IClock _clock = clock;

        public async Task<Result<NotificationPublishResult>> PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        {
            var type = NotificationTypes.Find(request.Type);
            if (type is null)
            {
                return NotificationErrors.DataInvalid;
            }

            var now = _clock.UtcNow;

            var built = Notification.Create(request.UserId, type.Code, request.Title, request.Content, request.ReferenceType, request.ReferenceId, request.SourceEventId, type.Mandatory, now);
            if (!built.TryGetValue(out var notification))
            {
                return Result<NotificationPublishResult>.Fail(built.Errors);
            }

            // The same event already notified this user.
            if (request.SourceEventId is { } source && await _notifications.AnyAsync(new NotificationBySourceSpecification(request.UserId, source, type.Code), cancellationToken))
            {
                return new NotificationPublishResult(false, false, false);
            }

            var inApp = true;
            var email = true;

            if (!type.Mandatory)
            {
                var preference = await _preferences.FirstOrDefaultAsync(new NotificationPreferenceSpecification(request.UserId, type.Code), cancellationToken);

                if (preference is not null)
                {
                    inApp = preference.InAppEnabled;
                    email = preference.EmailEnabled;
                }
            }

            if (!inApp && !email)
            {
                return new NotificationPublishResult(false, false, false);
            }

            if (inApp)
            {
                notification.DeliverInApp(now);
            }

            var emailQueued = false;

            if (email)
            {
                var address = await _contacts.GetEmailAsync(request.UserId, cancellationToken);

                if (string.IsNullOrWhiteSpace(address))
                {
                    notification.RecordEmail(NotificationDeliveryStatus.Failed, "The user has no email address.", now);
                }
                else if (await _emailQueue.TryEnqueueAsync(NotificationEmails.Build(_settings.AppName, address, notification.Title, notification.Content), cancellationToken))
                {
                    notification.RecordEmail(NotificationDeliveryStatus.Queued, null, now);
                    emailQueued = true;
                }
                else
                {
                    notification.RecordEmail(NotificationDeliveryStatus.Failed, "The email could not be queued.", now);
                }
            }

            _notifications.Add(notification);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<NotificationPublishResult>.Fail(saved.Errors);
            }

            if (inApp)
            {
                // The notification is saved. Telling a person who is online right now is a shortcut that can fail
                // without losing anything: they will see it in their list.
                var unread = await _notifications.CountAsync(new MyNotificationsSpecification(new PaginationQuery(1, 1), request.UserId, unreadOnly: true), cancellationToken);

                await _push.PushAsync(request.UserId, new NotificationDto(notification.Id, notification.Type, notification.Title, notification.Content, notification.ReferenceType, notification.ReferenceId, notification.CreatedAt, notification.ReadAt, notification.IsRead), unread, cancellationToken);
            }

            return new NotificationPublishResult(true, inApp, emailQueued);
        }
    }


}
