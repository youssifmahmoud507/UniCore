using System.Globalization;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Notifications
{
    public sealed class PaymentRecordedNotificationHandler(StudentNotifier notifier) : IDomainEventHandler<PaymentRecorded>
    {
        private readonly StudentNotifier _notifier = notifier;

        public Task<Result> HandleAsync(PaymentRecorded domainEvent, CancellationToken cancellationToken = default)
        {
            var amount = domainEvent.Amount.ToString("0.00", CultureInfo.InvariantCulture);
            var content = domainEvent.InvoiceFullyPaid ? $"We received your payment of {amount}. Your invoice is now fully paid." : $"We received your payment of {amount}.";

            return _notifier.NotifyAsync(domainEvent.StudentId, NotificationTypes.PaymentReceived, "Payment received", content, "Invoice", domainEvent.InvoiceId, domainEvent.EventId, cancellationToken);
        }
    }


}
