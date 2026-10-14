using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // Aggregate root with its lines. F2: issue only. Discounts arrive in F4, payments in F3.
    public sealed class Invoice : AggregateRoot
    {
        public const int MaxLines = 100;
        public const int MaxNumberLength = 30;

        private readonly List<InvoiceLine> _lines = [];

        private Invoice()
        {
        }

        public Guid StudentId { get; private set; }
        public string InvoiceNumber { get; private set; } = string.Empty;
        public DateOnly IssueDate { get; private set; }
        public DateOnly DueDate { get; private set; }
        public Money Subtotal { get; private set; }
        public Money DiscountAmount { get; private set; }
        public Money TotalAmount { get; private set; }
        public Money PaidAmount { get; private set; }
        public Money Balance { get; private set; }
        public InvoiceStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public IReadOnlyList<InvoiceLine> Lines => _lines.AsReadOnly();

        public static Result<Invoice> Issue(Guid studentId, string? invoiceNumber, DateOnly issueDate, DateOnly dueDate, IReadOnlyList<InvoiceLineInput> lines, DateTimeOffset now)
        {
            var number = invoiceNumber?.Trim();

            if (studentId == Guid.Empty
                || string.IsNullOrEmpty(number)
                || number.Length > MaxNumberLength
                || dueDate < issueDate)
            {
                return FinanceErrors.InvoiceDataInvalid;
            }

            if (lines.Count == 0)
            {
                return FinanceErrors.NoPendingCharges;
            }

            if (lines.Count > MaxLines)
            {
                return FinanceErrors.InvoiceTooManyLines;
            }

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                InvoiceNumber = number,
                IssueDate = issueDate,
                DueDate = dueDate,
                Status = InvoiceStatus.Issued,
                CreatedAt = now
            };

            var subtotal = Money.Zero;

            foreach (var input in lines)
            {
                var created = InvoiceLine.Create(invoice.Id, input.Description, input.Quantity, input.UnitPrice, input.StudentChargeId);
                if (!created.TryGetValue(out var line))
                {
                    return Result<Invoice>.Fail(created.Errors);
                }

                var sum = subtotal.Add(line.Amount);
                if (!sum.TryGetValue(out var next))
                {
                    return Result<Invoice>.Fail(sum.Errors);
                }

                subtotal = next;
                invoice._lines.Add(line);
            }

            invoice.Subtotal = subtotal;
            invoice.DiscountAmount = Money.Zero;
            invoice.TotalAmount = subtotal;
            invoice.PaidAmount = Money.Zero;
            invoice.Balance = subtotal;

            return invoice;
        }
    }


}
