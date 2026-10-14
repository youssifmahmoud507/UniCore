using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record InvoiceSummaryDto(Guid Id, Guid StudentId, string InvoiceNumber, DateOnly IssueDate, DateOnly DueDate, decimal TotalAmount, decimal PaidAmount, decimal Balance, InvoiceStatus Status);



}
