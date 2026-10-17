using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    // What a student sees of a payment: no staff account id.
    public sealed record MyPaymentDto(Guid Id, Guid InvoiceId, decimal Amount, DateOnly PaymentDate, PaymentMethod Method, string? Reference, decimal RefundedAmount, DateTimeOffset CreatedAt);


}
