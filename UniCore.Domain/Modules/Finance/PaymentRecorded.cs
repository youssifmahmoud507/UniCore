using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Finance
{
    // Raised by Invoice.RecordPayment. Delivered by the outbox after the payment is saved.
    // F5 uses it to release financial holds.
    public sealed record PaymentRecorded(Guid EventId, DateTimeOffset OccurredAt, Guid PaymentId, Guid InvoiceId, Guid StudentId, decimal Amount, DateOnly PaymentDate, bool InvoiceFullyPaid) : IDomainEvent;


}
