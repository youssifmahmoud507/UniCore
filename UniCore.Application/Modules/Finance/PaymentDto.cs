using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record PaymentDto(Guid Id, Guid InvoiceId, decimal Amount, DateOnly PaymentDate, PaymentMethod Method, string? Reference, Guid? RecordedByUserId, DateTimeOffset CreatedAt, decimal RefundCommittedAmount, decimal RefundedAmount);



}
