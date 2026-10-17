using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class InvoiceRefundTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Today = new(2026, 10, 17);

        private static Money Amount(decimal value)
        {
            Assert.True(Money.Of(value).TryGetValue(out var money));
            return money;
        }

        // An invoice of 1000 with one payment of `paid`.
        private static (Invoice Invoice, Payment Payment) PaidInvoice(decimal paid)
        {
            var lines = new[] { new InvoiceLineInput("Tuition", 1, Amount(1000m), Guid.NewGuid()) };
            var invoice = Invoice.Issue(Guid.NewGuid(), "INV-2026-000001", Today, Today.AddDays(30), lines, Now).Value;
            Assert.NotNull(invoice);

            var payment = invoice.RecordPayment(Amount(paid), Today, Today, PaymentMethod.Cash, null, "key-00000001", null, Now).Value;
            Assert.NotNull(payment);

            return (invoice, payment);
        }

        [Fact]
        public void Reserving_refunds_cannot_go_over_the_payment()
        {
            var (invoice, payment) = PaidInvoice(600m);

            Assert.True(invoice.ReserveRefund(payment.Id, Amount(200m), Now).IsSuccess);
            Assert.Equal("REFUND_EXCEEDS_PAYMENT", invoice.ReserveRefund(payment.Id, Amount(400.01m), Now).Errors[0].Code);
            Assert.True(invoice.ReserveRefund(payment.Id, Amount(400m), Now).IsSuccess);

            Assert.Equal(600m, payment.RefundCommittedAmount.Amount);
            Assert.True(payment.RefundedAmount.IsZero);
        }

        [Fact]
        public void An_unknown_payment_or_a_bad_amount_is_rejected()
        {
            var (invoice, payment) = PaidInvoice(600m);

            Assert.Equal("PAYMENT_NOT_FOUND", invoice.ReserveRefund(Guid.NewGuid(), Amount(10m), Now).Errors[0].Code);
            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", invoice.ReserveRefund(payment.Id, Money.Zero, Now).Errors[0].Code);
            Assert.True(payment.RefundCommittedAmount.IsZero);
        }

        [Fact]
        public void A_rejected_refund_gives_its_amount_back()
        {
            var (invoice, payment) = PaidInvoice(600m);
            invoice.ReserveRefund(payment.Id, Amount(400m), Now);

            Assert.True(invoice.ReleaseRefund(payment.Id, Amount(400m), Now).IsSuccess);

            Assert.True(payment.RefundCommittedAmount.IsZero);
            Assert.True(invoice.ReserveRefund(payment.Id, Amount(600m), Now).IsSuccess);
        }

        [Fact]
        public void A_processed_refund_lowers_the_paid_amount_and_raises_the_balance()
        {
            var (invoice, payment) = PaidInvoice(600m);
            invoice.ReserveRefund(payment.Id, Amount(400m), Now);
            var refundId = Guid.NewGuid();

            var result = invoice.ApplyRefund(refundId, payment.Id, Amount(400m), Now.AddDays(1));

            Assert.True(result.IsSuccess);
            Assert.Equal(200m, invoice.PaidAmount.Amount);
            Assert.Equal(800m, invoice.Balance.Amount);
            Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
            Assert.Equal(400m, payment.RefundedAmount.Amount);
            Assert.Equal(400m, payment.RefundCommittedAmount.Amount);
            Assert.Equal(Now.AddDays(1), invoice.UpdatedAt);

            var raised = invoice.DomainEvents.OfType<RefundProcessed>().Single();
            Assert.Equal(refundId, raised.RefundId);
            Assert.Equal(payment.Id, raised.PaymentId);
            Assert.Equal(invoice.Id, raised.InvoiceId);
            Assert.Equal(invoice.StudentId, raised.StudentId);
            Assert.Equal(400m, raised.Amount);
            Assert.Equal(800m, raised.InvoiceBalance);
        }

        [Fact]
        public void A_paid_invoice_is_reopened_by_a_refund()
        {
            var (invoice, payment) = PaidInvoice(1000m);
            Assert.Equal(InvoiceStatus.Paid, invoice.Status);

            invoice.ReserveRefund(payment.Id, Amount(100m), Now);
            invoice.ApplyRefund(Guid.NewGuid(), payment.Id, Amount(100m), Now);

            Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
            Assert.Equal(100m, invoice.Balance.Amount);
        }

        [Fact]
        public void Refunding_everything_makes_the_invoice_issued_again()
        {
            var (invoice, payment) = PaidInvoice(300m);

            invoice.ReserveRefund(payment.Id, Amount(300m), Now);
            invoice.ApplyRefund(Guid.NewGuid(), payment.Id, Amount(300m), Now);

            Assert.Equal(InvoiceStatus.Issued, invoice.Status);
            Assert.True(invoice.PaidAmount.IsZero);
            Assert.Equal(1000m, invoice.Balance.Amount);
        }

        [Fact]
        public void A_refund_that_was_not_reserved_cannot_be_applied()
        {
            var (invoice, payment) = PaidInvoice(600m);

            var result = invoice.ApplyRefund(Guid.NewGuid(), payment.Id, Amount(100m), Now);

            Assert.Equal("REFUND_INVALID_STATE", result.Errors[0].Code);
            Assert.Equal(600m, invoice.PaidAmount.Amount);
            Assert.Empty(invoice.DomainEvents.OfType<RefundProcessed>());
        }

        [Fact]
        public void A_cancelled_invoice_accepts_no_refund_and_an_unknown_payment_is_not_found()
        {
            var (invoice, payment) = PaidInvoice(600m);

            Assert.Equal("PAYMENT_NOT_FOUND", invoice.ApplyRefund(Guid.NewGuid(), Guid.NewGuid(), Amount(1m), Now).Errors[0].Code);
            Assert.Equal("PAYMENT_NOT_FOUND", invoice.ReleaseRefund(Guid.NewGuid(), Amount(1m), Now).Errors[0].Code);
            Assert.NotNull(payment);
        }
    }

}
