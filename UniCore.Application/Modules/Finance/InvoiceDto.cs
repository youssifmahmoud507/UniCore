using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record InvoiceDto(Guid Id, Guid StudentId, string InvoiceNumber, DateOnly IssueDate, DateOnly DueDate, decimal Subtotal, decimal DiscountAmount, decimal TotalAmount, decimal PaidAmount, decimal Balance, InvoiceStatus Status, IReadOnlyList<InvoiceLineDto> Lines, IReadOnlyList<InvoiceReductionDto> Reductions, IReadOnlyList<PaymentDto> Payments, DateTimeOffset CreatedAt);



}
