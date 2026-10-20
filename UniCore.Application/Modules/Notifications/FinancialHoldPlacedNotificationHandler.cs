using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Notifications
{
    public sealed class FinancialHoldPlacedNotificationHandler(StudentNotifier notifier) : IDomainEventHandler<FinancialHoldPlaced>
    {
        private readonly StudentNotifier _notifier = notifier;

        public Task<Result> HandleAsync(FinancialHoldPlaced domainEvent, CancellationToken cancellationToken = default) => _notifier.NotifyAsync(domainEvent.StudentId, NotificationTypes.FinancialHoldPlaced, "A financial hold was placed on your account", $"This blocks: {domainEvent.Areas}. Pay your balance or contact the finance office to have it released.", "FinancialHold", domainEvent.HoldId, domainEvent.EventId, cancellationToken);
    }


}
