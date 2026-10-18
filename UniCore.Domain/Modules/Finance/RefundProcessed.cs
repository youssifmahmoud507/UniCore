using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Finance
{
    // Raised by Invoice.ApplyRefund. Delivered by the outbox. F5 uses it to re-check financial holds.
    public sealed record RefundProcessed(
        Guid EventId,
        DateTimeOffset OccurredAt,
        Guid RefundId,
        Guid PaymentId,
        Guid InvoiceId,
        Guid StudentId,
        decimal Amount,
        decimal InvoiceBalance) : IDomainEvent;



}
