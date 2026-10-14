using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class StudentChargeTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Due = new(2026, 11, 14);

        private static Money Amount(decimal value)
        {
            Assert.True(Money.Of(value).TryGetValue(out var money));
            return money;
        }

        private static StudentCharge NewCharge(decimal amount = 1000m, string? description = "Tuition")
        {
            var result = StudentCharge.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), description, Amount(amount), Due, Now);
            Assert.True(result.TryGetValue(out var charge));
            Assert.NotNull(charge);
            return charge;
        }

        [Fact]
        public void A_new_charge_is_pending_and_keeps_its_snapshot()
        {
            var charge = NewCharge(1250.50m, "  Tuition  ");

            Assert.NotEqual(Guid.Empty, charge.Id);
            Assert.Equal(ChargeStatus.Pending, charge.Status);
            Assert.Equal("Tuition", charge.Description);
            Assert.Equal(1250.50m, charge.Amount.Amount);
            Assert.Null(charge.InvoiceId);
            Assert.Equal(Due, charge.DueDate);
        }

        [Fact]
        public void Empty_ids_or_a_blank_description_are_rejected()
        {
            var money = Amount(10m);

            Assert.Equal(FinanceErrors.ChargeDataInvalid.Code, StudentCharge.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "x", money, Due, Now).Error?.Code);
            Assert.Equal(FinanceErrors.ChargeDataInvalid.Code, StudentCharge.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "x", money, Due, Now).Error?.Code);
            Assert.Equal(FinanceErrors.ChargeDataInvalid.Code, StudentCharge.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "   ", money, Due, Now).Error?.Code);
            Assert.Equal(FinanceErrors.ChargeDataInvalid.Code, StudentCharge.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', StudentCharge.MaxDescriptionLength + 1), money, Due, Now).Error?.Code);
        }

        [Fact]
        public void A_zero_amount_is_rejected()
        {
            var result = StudentCharge.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "x", Money.Zero, Due, Now);

            Assert.True(result.IsFailure);
            Assert.Equal(FinanceErrors.AmountMustBePositive.Code, result.Error?.Code);
        }

        [Fact]
        public void A_pending_charge_can_be_invoiced_once()
        {
            var charge = NewCharge();
            var invoiceId = Guid.NewGuid();

            Assert.True(charge.MarkInvoiced(invoiceId, Now).IsSuccess);
            Assert.Equal(ChargeStatus.Invoiced, charge.Status);
            Assert.Equal(invoiceId, charge.InvoiceId);

            var again = charge.MarkInvoiced(Guid.NewGuid(), Now);
            Assert.Equal(FinanceErrors.ChargeNotPending.Code, again.Error?.Code);
            Assert.Equal(invoiceId, charge.InvoiceId);
        }

        [Fact]
        public void An_empty_invoice_id_is_rejected()
        {
            var charge = NewCharge();

            Assert.Equal(FinanceErrors.ChargeDataInvalid.Code, charge.MarkInvoiced(Guid.Empty, Now).Error?.Code);
            Assert.Equal(ChargeStatus.Pending, charge.Status);
        }

        [Fact]
        public void Only_a_pending_charge_can_be_cancelled()
        {
            var pending = NewCharge();
            Assert.True(pending.Cancel(Now).IsSuccess);
            Assert.Equal(ChargeStatus.Cancelled, pending.Status);
            Assert.Equal(FinanceErrors.ChargeNotPending.Code, pending.Cancel(Now).Error?.Code);

            var invoiced = NewCharge();
            Assert.True(invoiced.MarkInvoiced(Guid.NewGuid(), Now).IsSuccess);
            Assert.Equal(FinanceErrors.ChargeNotPending.Code, invoiced.Cancel(Now).Error?.Code);
        }
    }


}
