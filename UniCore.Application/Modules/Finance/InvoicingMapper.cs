using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public static class InvoicingMapper
    {
        public static StudentChargeDto ToDto(StudentCharge c) => new(c.Id, c.StudentId, c.AcademicSemesterId, c.FeeDefinitionId, c.Description, c.Amount.Amount, c.DueDate, c.Status, c.InvoiceId, c.CreatedAt, c.ServiceReference);
        public static PaymentDto ToDto(Payment p) => new(p.Id, p.InvoiceId, p.Amount.Amount, p.PaymentDate, p.Method, p.Reference, p.RecordedByUserId, p.CreatedAt, p.RefundCommittedAmount.Amount, p.RefundedAmount.Amount);

        public static InvoiceDto ToDto(Invoice i) => new(i.Id, i.StudentId, i.InvoiceNumber, i.IssueDate, i.DueDate, i.Subtotal.Amount, i.DiscountAmount.Amount, i.TotalAmount.Amount, i.PaidAmount.Amount, i.Balance.Amount, i.Status,
            [.. i.Lines.Select(l => new InvoiceLineDto(l.Id, l.Description, l.Quantity, l.UnitPrice.Amount, l.Amount.Amount, l.StudentChargeId))],
            [.. i.Reductions.OrderBy(r => (int)r.Kind).ThenBy(r => (int)r.Type).Select(r => ReductionsMapper.ToDto(r))],
            [.. i.Payments.OrderBy(p => p.CreatedAt).Select(p => ToDto(p))], i.CreatedAt);

        public static InvoiceSummaryDto ToSummary(Invoice i) => new(i.Id, i.StudentId, i.InvoiceNumber, i.IssueDate, i.DueDate, i.TotalAmount.Amount, i.PaidAmount.Amount, i.Balance.Amount, i.Status);
    }


}
