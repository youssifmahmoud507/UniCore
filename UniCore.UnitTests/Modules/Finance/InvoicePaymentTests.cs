using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class InvoicePaymentTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Today = new(2026, 10, 14);
        private static readonly DateOnly Due = new(2026, 11, 14);
        private static readonly Guid User = Guid.NewGuid();

        private static Money Amount(decimal value)
        {
            Assert.True(Money.Of(value).TryGetValue(out var money));
            return money;
        }

        private static Invoice NewInvoice(decimal total = 1000m)
        {
            var lines = new[] { new InvoiceLineInput("Tuition", 1, Amount(total), Guid.NewGuid()) };
            var result = Invoice.Issue(Guid.NewGuid(), "INV-2026-000001", Today, Due, lines, Now);
            Assert.True(result.TryGetValue(out var invoice));
            Assert.NotNull(invoice);
            return invoice;
        }

        private static Result<Payment> Pay(Invoice invoice, decimal amount, PaymentMethod method = PaymentMethod.Cash, string? reference = null, string key = "key-00000001", DateOnly? date = null, DateOnly? today = null)
            => invoice.RecordPayment(Amount(amount), date ?? Today, today ?? Today, method, reference, key, User, Now);

        // ---------- payments ----------

        [Fact]
        public void A_partial_payment_makes_the_invoice_partially_paid()
        {
            var invoice = NewInvoice();

            var result = Pay(invoice, 400m);

            Assert.True(result.TryGetValue(out var payment));
            Assert.NotNull(payment);
            Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
            Assert.Equal(400m, invoice.PaidAmount.Amount);
            Assert.Equal(600m, invoice.Balance.Amount);
            Assert.Single(invoice.Payments);
            Assert.Equal(invoice.Id, payment.InvoiceId);
            Assert.Equal(User, payment.RecordedByUserId);
            Assert.Equal(Now, invoice.UpdatedAt);
        }

        [Fact]
        public void Paying_the_rest_makes_the_invoice_paid()
        {
            var invoice = NewInvoice();
            Pay(invoice, 400m, key: "key-00000001");

            var result = Pay(invoice, 600m, key: "key-00000002");

            Assert.True(result.IsSuccess);
            Assert.Equal(InvoiceStatus.Paid, invoice.Status);
            Assert.Equal(1000m, invoice.PaidAmount.Amount);
            Assert.True(invoice.Balance.IsZero);
            Assert.Equal(2, invoice.Payments.Count);
        }

        [Fact]
        public void The_exact_balance_in_one_payment_is_fine()
        {
            var invoice = NewInvoice(250.50m);

            Assert.True(Pay(invoice, 250.50m).IsSuccess);

            Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        }

        [Fact]
        public void Each_payment_raises_an_event_and_the_last_one_says_the_invoice_is_fully_paid()
        {
            var invoice = NewInvoice();

            Pay(invoice, 400m, key: "key-00000001");
            Pay(invoice, 600m, key: "key-00000002");

            var events = invoice.DomainEvents.OfType<PaymentRecorded>().ToList();
            Assert.Equal(2, events.Count);
            Assert.Equal(400m, events[0].Amount);
            Assert.False(events[0].InvoiceFullyPaid);
            Assert.True(events[1].InvoiceFullyPaid);
            Assert.All(events, e => Assert.Equal(invoice.Id, e.InvoiceId));
            Assert.All(events, e => Assert.Equal(invoice.StudentId, e.StudentId));
        }

        [Fact]
        public void Paying_more_than_the_balance_is_rejected_and_changes_nothing()
        {
            var invoice = NewInvoice();
            Pay(invoice, 400m, key: "key-00000001");
            var eventsBefore = invoice.DomainEvents.Count;

            var result = Pay(invoice, 600.01m, key: "key-00000002");

            Assert.Equal("PAYMENT_EXCEEDS_BALANCE", result.Errors[0].Code);
            Assert.Equal(400m, invoice.PaidAmount.Amount);
            Assert.Equal(600m, invoice.Balance.Amount);
            Assert.Single(invoice.Payments);
            Assert.Equal(eventsBefore, invoice.DomainEvents.Count);
        }

        [Fact]
        public void A_paid_invoice_accepts_no_more_payments()
        {
            var invoice = NewInvoice();
            Pay(invoice, 1000m);

            var result = Pay(invoice, 1m, key: "key-00000002");

            Assert.Equal("INVOICE_ALREADY_PAID", result.Errors[0].Code);
        }

        [Fact]
        public void A_zero_or_negative_payment_is_rejected()
        {
            var invoice = NewInvoice();

            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", invoice.RecordPayment(Money.Zero, Today, Today, PaymentMethod.Cash, null, "key-00000001", User, Now).Errors[0].Code);
            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", Pay(invoice, -5m).Errors[0].Code);
            Assert.Empty(invoice.Payments);
        }

        [Fact]
        public void The_payment_date_cannot_be_in_the_future_or_before_the_invoice()
        {
            var invoice = NewInvoice();

            Assert.Equal("PAYMENT_DATE_INVALID", Pay(invoice, 10m, date: Today.AddDays(1)).Errors[0].Code);
            Assert.Equal("PAYMENT_DATE_INVALID", Pay(invoice, 10m, date: Today.AddDays(-1)).Errors[0].Code);
            Assert.True(Pay(invoice, 10m, date: Today, key: "key-00000002").IsSuccess);
            Assert.Single(invoice.Payments);
        }

        [Fact]
        public void A_bank_transfer_or_a_cheque_needs_a_reference_and_cash_does_not()
        {
            var invoice = NewInvoice();

            Assert.Equal("PAYMENT_REFERENCE_REQUIRED", Pay(invoice, 10m, PaymentMethod.BankTransfer).Errors[0].Code);
            Assert.Equal("PAYMENT_REFERENCE_REQUIRED", Pay(invoice, 10m, PaymentMethod.Cheque, "   ").Errors[0].Code);
            Assert.Empty(invoice.Payments);

            var transfer = Pay(invoice, 10m, PaymentMethod.BankTransfer, "  TRX-9981  ", "key-00000002");
            Assert.Equal("TRX-9981", transfer.Value?.Reference);
            Assert.True(Pay(invoice, 10m, PaymentMethod.Cash, null, "key-00000003").IsSuccess);
        }

        [Fact]
        public void A_reference_that_is_too_long_or_an_unknown_method_is_invalid()
        {
            var invoice = NewInvoice();

            Assert.Equal("PAYMENT_DATA_INVALID", Pay(invoice, 10m, PaymentMethod.Cash, new string('R', Payment.MaxReferenceLength + 1)).Errors[0].Code);
            Assert.Equal("PAYMENT_DATA_INVALID", Pay(invoice, 10m, (PaymentMethod)99).Errors[0].Code);
        }

        [Fact]
        public void A_bad_key_is_rejected_and_the_invoice_stays_untouched()
        {
            var invoice = NewInvoice();

            var result = Pay(invoice, 100m, key: "bad");

            Assert.Equal("IDEMPOTENCY_KEY_INVALID", result.Errors[0].Code);
            Assert.Equal(InvoiceStatus.Issued, invoice.Status);
            Assert.True(invoice.PaidAmount.IsZero);
            Assert.Equal(1000m, invoice.Balance.Amount);
            Assert.Empty(invoice.Payments);
            Assert.Empty(invoice.DomainEvents);
        }

        // ---------- cancellation ----------

        [Fact]
        public void An_unpaid_invoice_can_be_cancelled_and_nothing_is_owed_any_more()
        {
            var invoice = NewInvoice();

            var result = invoice.Cancel(Now.AddDays(1));

            Assert.True(result.IsSuccess);
            Assert.Equal(InvoiceStatus.Cancelled, invoice.Status);
            Assert.True(invoice.Balance.IsZero);
            Assert.Equal(1000m, invoice.TotalAmount.Amount);
            Assert.Equal(Now.AddDays(1), invoice.UpdatedAt);
        }

        [Fact]
        public void An_invoice_with_a_payment_cannot_be_cancelled()
        {
            var partial = NewInvoice();
            Pay(partial, 100m);

            var paid = NewInvoice();
            Pay(paid, 1000m);

            Assert.Equal("INVOICE_HAS_PAYMENTS", partial.Cancel(Now).Errors[0].Code);
            Assert.Equal("INVOICE_HAS_PAYMENTS", paid.Cancel(Now).Errors[0].Code);
            Assert.Equal(InvoiceStatus.PartiallyPaid, partial.Status);
        }

        [Fact]
        public void A_cancelled_invoice_cannot_be_cancelled_again_or_paid()
        {
            var invoice = NewInvoice();
            invoice.Cancel(Now);

            Assert.Equal("INVOICE_CANCELLED", invoice.Cancel(Now).Errors[0].Code);
            Assert.Equal("INVOICE_CANCELLED", Pay(invoice, 10m).Errors[0].Code);
        }

        // ---------- the charge that goes with a cancelled invoice ----------

        [Fact]
        public void A_charge_is_cancelled_with_its_invoice_only()
        {
            var invoiceId = Guid.NewGuid();
            var created = StudentCharge.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Tuition", Amount(500m), Due, Now);
            Assert.True(created.TryGetValue(out var charge));
            Assert.NotNull(charge);

            Assert.Equal("STUDENT_CHARGE_NOT_INVOICED", charge.CancelWithInvoice(invoiceId, Now).Errors[0].Code);

            charge.MarkInvoiced(invoiceId, Now);

            Assert.Equal("STUDENT_CHARGE_NOT_INVOICED", charge.CancelWithInvoice(Guid.NewGuid(), Now).Errors[0].Code);
            Assert.True(charge.CancelWithInvoice(invoiceId, Now.AddDays(1)).IsSuccess);
            Assert.Equal(ChargeStatus.Cancelled, charge.Status);
            Assert.Equal(invoiceId, charge.InvoiceId);
        }
    }

}
