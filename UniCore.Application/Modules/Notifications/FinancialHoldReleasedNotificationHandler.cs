using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Notifications
{
    public sealed class FinancialHoldReleasedNotificationHandler(StudentNotifier notifier) : IDomainEventHandler<FinancialHoldReleased>
    {
        private readonly StudentNotifier _notifier = notifier;

        public Task<Result> HandleAsync(FinancialHoldReleased domainEvent, CancellationToken cancellationToken = default)
            => _notifier.NotifyAsync(domainEvent.StudentId, NotificationTypes.FinancialHoldReleased, "Your financial hold was released", "The financial hold on your account was released. You can register and use the services again.", "FinancialHold", domainEvent.HoldId, domainEvent.EventId, cancellationToken);
    }


}
