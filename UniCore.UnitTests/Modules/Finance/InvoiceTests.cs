using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class InvoiceTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Issue = new(2026, 10, 14);
        private static readonly DateOnly Due = new(2026, 11, 14);

        private static Money Amount(decimal value)
        {
            Assert.True(Money.Of(value).TryGetValue(out var money));
            return money;
        }

        private static InvoiceLineInput Line(decimal price, int quantity = 1, string description = "Tuition", Guid? charge = null)
            => new(description, quantity, Amount(price), charge ?? Guid.NewGuid());

        private static Invoice NewInvoice(params InvoiceLineInput[] lines)
        {
            var result = Invoice.Issue(Guid.NewGuid(), "INV-2026-000001", Issue, Due, lines, Now);
            Assert.True(result.TryGetValue(out var invoice));
            Assert.NotNull(invoice);
            return invoice;
        }

        [Fact]
        public void An_issued_invoice_adds_up_its_lines()
        {
            var invoice = NewInvoice(Line(1000m), Line(250.50m), Line(10m, 3));

            Assert.Equal(InvoiceStatus.Issued, invoice.Status);
            Assert.Equal(3, invoice.Lines.Count);
            Assert.Equal(1280.50m, invoice.Subtotal.Amount);
            Assert.Equal(0m, invoice.DiscountAmount.Amount);
            Assert.Equal(1280.50m, invoice.TotalAmount.Amount);
            Assert.Equal(0m, invoice.PaidAmount.Amount);
            Assert.Equal(1280.50m, invoice.Balance.Amount);
            Assert.Equal("INV-2026-000001", invoice.InvoiceNumber);
        }

        [Fact]
        public void A_line_amount_is_price_times_quantity()
        {
            var invoice = NewInvoice(Line(33.33m, 3));

            Assert.Equal(99.99m, invoice.Lines[0].Amount.Amount);
            Assert.Equal(3, invoice.Lines[0].Quantity);
        }

        [Fact]
        public void Every_line_points_to_its_invoice_and_charge()
        {
            var charge = Guid.NewGuid();
            var invoice = NewInvoice(Line(100m, charge: charge));

            Assert.Equal(invoice.Id, invoice.Lines[0].InvoiceId);
            Assert.Equal(charge, invoice.Lines[0].StudentChargeId);
        }

        [Fact]
        public void An_invoice_without_lines_is_rejected()
        {
            var result = Invoice.Issue(Guid.NewGuid(), "INV-2026-000001", Issue, Due, [], Now);

            Assert.True(result.IsFailure);
            Assert.Equal(FinanceErrors.NoPendingCharges.Code, result.Error?.Code);
        }

        [Fact]
        public void Too_many_lines_are_rejected()
        {
            var lines = Enumerable.Range(0, Invoice.MaxLines + 1).Select(_ => Line(1m)).ToList();

            var result = Invoice.Issue(Guid.NewGuid(), "INV-2026-000001", Issue, Due, lines, Now);

            Assert.Equal(FinanceErrors.InvoiceTooManyLines.Code, result.Error?.Code);
        }

        [Fact]
        public void A_due_date_before_the_issue_date_is_rejected()
        {
            var result = Invoice.Issue(Guid.NewGuid(), "INV-2026-000001", Issue, Issue.AddDays(-1), [Line(10m)], Now);

            Assert.Equal(FinanceErrors.InvoiceDataInvalid.Code, result.Error?.Code);
        }

        [Fact]
        public void A_missing_student_or_number_is_rejected()
        {
            Assert.Equal(FinanceErrors.InvoiceDataInvalid.Code, Invoice.Issue(Guid.Empty, "INV-1", Issue, Due, [Line(10m)], Now).Error?.Code);
            Assert.Equal(FinanceErrors.InvoiceDataInvalid.Code, Invoice.Issue(Guid.NewGuid(), "  ", Issue, Due, [Line(10m)], Now).Error?.Code);
            Assert.Equal(FinanceErrors.InvoiceDataInvalid.Code, Invoice.Issue(Guid.NewGuid(), new string('9', Invoice.MaxNumberLength + 1), Issue, Due, [Line(10m)], Now).Error?.Code);
        }

        [Fact]
        public void A_bad_line_is_rejected_and_no_invoice_is_created()
        {
            var blank = Invoice.Issue(Guid.NewGuid(), "INV-1", Issue, Due, [Line(10m, description: "  ")], Now);
            Assert.Equal(FinanceErrors.InvoiceDataInvalid.Code, blank.Error?.Code);

            var zeroQuantity = Invoice.Issue(Guid.NewGuid(), "INV-1", Issue, Due, [Line(10m, quantity: 0)], Now);
            Assert.Equal(FinanceErrors.InvoiceDataInvalid.Code, zeroQuantity.Error?.Code);

            var zeroPrice = Invoice.Issue(Guid.NewGuid(), "INV-1", Issue, Due, [new InvoiceLineInput("Tuition", 1, Money.Zero, null)], Now);
            Assert.Equal(FinanceErrors.AmountMustBePositive.Code, zeroPrice.Error?.Code);
        }
    }


}
