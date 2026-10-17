using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class InvoiceReductionTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Today = new(2026, 10, 17);

        private static Money Amount(decimal value)
        {
            Assert.True(Money.Of(value).TryGetValue(out var money));
            return money;
        }

        private static Invoice Issue(decimal total, params ReductionInput[] reductions)
        {
            var lines = new[] { new InvoiceLineInput("Tuition", 1, Amount(total), Guid.NewGuid()) };
            var result = Invoice.Issue(Guid.NewGuid(), "INV-2026-000001", Today, Today.AddDays(30), lines, Now, reductions);
            Assert.True(result.TryGetValue(out var invoice));
            Assert.NotNull(invoice);
            return invoice;
        }

        [Fact]
        public void An_invoice_without_reductions_is_unchanged()
        {
            var invoice = Issue(1000m);

            Assert.Equal(1000m, invoice.Subtotal.Amount);
            Assert.True(invoice.DiscountAmount.IsZero);
            Assert.Equal(1000m, invoice.TotalAmount.Amount);
            Assert.Equal(1000m, invoice.Balance.Amount);
            Assert.Equal(InvoiceStatus.Issued, invoice.Status);
            Assert.Empty(invoice.Reductions);
        }

        [Fact]
        public void A_scholarship_and_a_discount_lower_the_total_and_are_kept_on_the_invoice()
        {
            var scholarship = new ReductionInput(ReductionKind.Scholarship, Guid.NewGuid(), "Merit A", ReductionType.Percentage, 50m);
            var discount = new ReductionInput(ReductionKind.Discount, Guid.NewGuid(), "Early bird", ReductionType.FixedAmount, 100m);

            var invoice = Issue(1000m, discount, scholarship);

            Assert.Equal(1000m, invoice.Subtotal.Amount);
            Assert.Equal(600m, invoice.DiscountAmount.Amount);
            Assert.Equal(400m, invoice.TotalAmount.Amount);
            Assert.Equal(400m, invoice.Balance.Amount);
            Assert.Equal(InvoiceStatus.Issued, invoice.Status);

            Assert.Equal(2, invoice.Reductions.Count);
            var first = invoice.Reductions.First(r => r.Kind == ReductionKind.Scholarship);
            Assert.Equal("Merit A", first.Name);
            Assert.Equal(50m, first.Value);
            Assert.Equal(500m, first.Amount.Amount);
            Assert.Equal(scholarship.SourceId, first.SourceId);
        }

        [Fact]
        public void A_full_scholarship_makes_an_invoice_that_is_already_paid_and_can_still_be_cancelled()
        {
            var invoice = Issue(800m, new ReductionInput(ReductionKind.Scholarship, Guid.NewGuid(), "Full", ReductionType.Percentage, 100m));

            Assert.True(invoice.TotalAmount.IsZero);
            Assert.True(invoice.Balance.IsZero);
            Assert.Equal(InvoiceStatus.Paid, invoice.Status);
            Assert.Equal(800m, invoice.DiscountAmount.Amount);

            Assert.True(invoice.Cancel(Now.AddDays(1)).IsSuccess);
            Assert.Equal(InvoiceStatus.Cancelled, invoice.Status);
        }

        [Fact]
        public void The_total_never_goes_below_zero()
        {
            var invoice = Issue(100m,
                new ReductionInput(ReductionKind.Scholarship, Guid.NewGuid(), "Big", ReductionType.FixedAmount, 500m),
                new ReductionInput(ReductionKind.Discount, Guid.NewGuid(), "Extra", ReductionType.Percentage, 10m));

            Assert.True(invoice.TotalAmount.IsZero);
            Assert.Equal(100m, invoice.DiscountAmount.Amount);
            Assert.Single(invoice.Reductions);
        }

        [Fact]
        public void An_invoice_with_a_payment_cannot_be_cancelled_even_if_it_was_fully_refunded()
        {
            var invoice = Issue(500m);
            var payment = invoice.RecordPayment(Amount(500m), Today, Today, PaymentMethod.Cash, null, "key-00000001", null, Now).Value;
            Assert.NotNull(payment);
            invoice.ReserveRefund(payment.Id, Amount(500m), Now);
            invoice.ApplyRefund(Guid.NewGuid(), payment.Id, Amount(500m), Now);

            Assert.Equal(InvoiceStatus.Issued, invoice.Status);
            Assert.Equal("INVOICE_HAS_PAYMENTS", invoice.Cancel(Now).Errors[0].Code);
        }
    }
}
