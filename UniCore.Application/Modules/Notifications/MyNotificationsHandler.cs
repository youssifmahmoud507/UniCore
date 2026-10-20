using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Notifications.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Notifications;

namespace UniCore.Application.Modules.Notifications
{
    // A user's own notifications. Any logged-in user may use it; every query is limited to that user.
    public sealed class MyNotificationsHandler(ICurrentUser user, IAggregateRepository<Notification> notifications, IAggregateRepository<NotificationPreference> preferences, IUnitOfWork unitOfWork, IClock clock)
    {
        // Mark-all works in rounds of 100, at most this many rounds (so at most 1000 per call).
        private const int MaxMarkAllRounds = 10;

        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Notification> _notifications = notifications;
        private readonly IAggregateRepository<NotificationPreference> _preferences = preferences;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;

        private static NotificationDto ToDto(Notification n) => new(n.Id, n.Type, n.Title, n.Content, n.ReferenceType, n.ReferenceId, n.CreatedAt, n.ReadAt, n.IsRead);

        public async Task<Result<PagedResult<NotificationDto>>> ListAsync(PaginationQuery query, bool unreadOnly, CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var specification = new MyNotificationsSpecification(query, userId, unreadOnly);

            var items = await _notifications.ListAsync(specification, cancellationToken);
            var total = await _notifications.CountAsync(specification, cancellationToken);

            return PagedResult<NotificationDto>.Create(items.Select(ToDto).ToList(), query, total);
        }

        public async Task<Result<UnreadCountDto>> UnreadCountAsync(CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var count = await _notifications.CountAsync(new MyNotificationsSpecification(new PaginationQuery(1, 1), userId, unreadOnly: true), cancellationToken);

            return new UnreadCountDto(count);
        }

        public async Task<Result<NotificationDto>> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var notification = await _notifications.FirstOrDefaultAsync(new NotificationByIdSpecification(notificationId, userId, tracking: true), cancellationToken);
            if (notification is null)
            {
                return NotificationErrors.NotFound;
            }

            notification.MarkRead(_clock.UtcNow);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<NotificationDto>.Fail(saved.Errors);
            }

            return ToDto(notification);
        }

        public async Task<Result<MarkedReadDto>> MarkAllReadAsync(CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var marked = 0;
            var now = _clock.UtcNow;

            for (var round = 0; round < MaxMarkAllRounds; round++)
            {
                var batch = await _notifications.ListAsync(new UnreadNotificationsSpecification(new PaginationQuery(1, PaginationQuery.MaxPageSize), userId), cancellationToken);

                if (batch.Count == 0)
                {
                    break;
                }

                foreach (var notification in batch)
                {
                    notification.MarkRead(now);
                }

                var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
                if (saved.IsFailure)
                {
                    return Result<MarkedReadDto>.Fail(saved.Errors);
                }

                marked += batch.Count;

                if (batch.Count < PaginationQuery.MaxPageSize)
                {
                    break;
                }
            }

            return new MarkedReadDto(marked);
        }

        // Every type the system sends, with the user's choice (both on when they never chose).
        public async Task<Result<IReadOnlyList<NotificationPreferenceDto>>> GetPreferencesAsync(CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var chosen = await _preferences.ListAsync(new PreferencesOfUserSpecification(userId), cancellationToken);

            IReadOnlyList<NotificationPreferenceDto> result =
            [
                .. NotificationTypes.All.Select(t =>
                {
                    var preference = chosen.FirstOrDefault(p => p.NotificationType == t.Code);

                    return new NotificationPreferenceDto(t.Code, t.Name, t.Mandatory, t.Mandatory || (preference?.InAppEnabled ?? true), t.Mandatory || (preference?.EmailEnabled ?? true));
                })
            ];

            return Result<IReadOnlyList<NotificationPreferenceDto>>.Ok(result);
        }

        public async Task<Result<NotificationPreferenceDto>> SetPreferenceAsync(string? type, bool inAppEnabled, bool emailEnabled, CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var info = NotificationTypes.Find(type);
            if (info is null)
            {
                return NotificationErrors.PreferenceInvalid;
            }

            if (info.Mandatory)
            {
                return NotificationErrors.PreferenceMandatory;
            }

            var now = _clock.UtcNow;

            var preference = await _preferences.FirstOrDefaultAsync(new NotificationPreferenceSpecification(userId, info.Code, tracking: true), cancellationToken);

            if (preference is null)
            {
                var created = NotificationPreference.Create(userId, info.Code, inAppEnabled, emailEnabled, now);
                if (!created.TryGetValue(out preference))
                {
                    return Result<NotificationPreferenceDto>.Fail(created.Errors);
                }

                _preferences.Add(preference);
            }
            else
            {
                preference.Update(inAppEnabled, emailEnabled, now);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<NotificationPreferenceDto>.Fail(saved.Errors);
            }

            return new NotificationPreferenceDto(info.Code, info.Name, info.Mandatory, preference.InAppEnabled, preference.EmailEnabled);
        }
    }


}
