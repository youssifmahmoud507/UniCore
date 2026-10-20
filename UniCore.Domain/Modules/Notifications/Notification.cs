using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Notifications
{
    // One message for one user. It shows in the user's in-app list when it has an InApp delivery.
    // SourceEventId (the domain event that caused it) with the user and type makes a producer safe to retry:
    // the outbox delivers events at least once, so the same event must not notify the same user twice.
    public sealed class Notification : AggregateRoot
    {
        public const int MinTypeLength = 3;
        public const int MaxTypeLength = 60;
        public const int MaxTitleLength = 150;
        public const int MaxContentLength = 1000;
        public const int MaxReferenceTypeLength = 50;

        private readonly List<NotificationDelivery> _deliveries = [];

        private Notification()
        {
        }

        public Guid UserId { get; private set; }
        public string Type { get; private set; } = string.Empty;
        public string Title { get; private set; } = string.Empty;
        public string Content { get; private set; } = string.Empty;
        public string? ReferenceType { get; private set; }
        public Guid? ReferenceId { get; private set; }
        public Guid? SourceEventId { get; private set; }
        public bool IsMandatory { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? ReadAt { get; private set; }

        public IReadOnlyList<NotificationDelivery> Deliveries => _deliveries.AsReadOnly();

        public bool IsRead => ReadAt is not null;

        public bool HasInApp => _deliveries.Any(d => d.Channel == NotificationChannel.InApp);

        // Upper case letters, digits and underscore. Null means invalid.
        public static string? NormalizeType(string? type)
        {
            var clean = type?.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(clean) || clean.Length < MinTypeLength || clean.Length > MaxTypeLength)
            {
                return null;
            }

            return clean.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_') ? clean : null;
        }

        public static Result<Notification> Create(Guid userId, string? type, string? title, string? content, string? referenceType, Guid? referenceId, Guid? sourceEventId, bool mandatory, DateTimeOffset now)
        {
            var cleanType = NormalizeType(type);
            var cleanTitle = title?.Trim();
            var cleanContent = content?.Trim();
            var cleanReferenceType = string.IsNullOrWhiteSpace(referenceType) ? null : referenceType.Trim();

            if (userId == Guid.Empty
                || cleanType is null
                || string.IsNullOrEmpty(cleanTitle)
                || cleanTitle.Length > MaxTitleLength
                || string.IsNullOrEmpty(cleanContent)
                || cleanContent.Length > MaxContentLength
                || (cleanReferenceType is { Length: > MaxReferenceTypeLength })
                || (referenceId is { } reference && reference == Guid.Empty)
                || (sourceEventId is { } source && source == Guid.Empty))
            {
                return NotificationErrors.DataInvalid;
            }

            return new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = cleanType,
                Title = cleanTitle,
                Content = cleanContent,
                ReferenceType = cleanReferenceType,
                ReferenceId = referenceId,
                SourceEventId = sourceEventId,
                IsMandatory = mandatory,
                CreatedAt = now
            };
        }

        // The notification is saved in the user's list. Doing it twice changes nothing.
        public void DeliverInApp(DateTimeOffset now)
        {
            if (_deliveries.Any(d => d.Channel == NotificationChannel.InApp))
            {
                return;
            }

            _deliveries.Add(NotificationDelivery.Create(Id, NotificationChannel.InApp, NotificationDeliveryStatus.Sent, null, now));
        }

        // Queued: the outbox has the email. Failed: it could not be queued (the error says why).
        public void RecordEmail(NotificationDeliveryStatus status, string? error, DateTimeOffset now)
        {
            if (_deliveries.Any(d => d.Channel == NotificationChannel.Email))
            {
                return;
            }

            _deliveries.Add(NotificationDelivery.Create(Id, NotificationChannel.Email, status, error, now));
        }

        // Reading twice keeps the first time.
        public void MarkRead(DateTimeOffset now)
        {
            ReadAt ??= now;
        }
    }




}
