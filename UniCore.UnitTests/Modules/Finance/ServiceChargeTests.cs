using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class ServiceChargeTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Due = new(2026, 10, 31);

        private static Money Amount(decimal value)
        {
            Assert.True(Money.Of(value).TryGetValue(out var money));
            return money;
        }

        private static StudentCharge NewService(string? reference = "REQ-1", decimal amount = 150m)
        {
            var result = StudentCharge.CreateService(Guid.NewGuid(), Guid.NewGuid(), "Transcript", Amount(amount), Due, reference, Now);
            Assert.True(result.TryGetValue(out var charge));
            Assert.NotNull(charge);
            return charge;
        }

        [Fact]
        public void A_service_charge_has_no_semester_and_no_structure_item()
        {
            var charge = NewService("  REQ-77  ");

            Assert.True(charge.IsService);
            Assert.Null(charge.AcademicSemesterId);
            Assert.Null(charge.FeeStructureItemId);
            Assert.Equal("REQ-77", charge.ServiceReference);
            Assert.Equal(ChargeStatus.Pending, charge.Status);
            Assert.Equal(150m, charge.Amount.Amount);
        }

        [Fact]
        public void A_semester_charge_is_not_a_service_charge()
        {
            var result = StudentCharge.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Tuition", Amount(10m), Due, Now);

            Assert.True(result.TryGetValue(out var charge));
            Assert.False(charge!.IsService);
            Assert.Null(charge.ServiceReference);
        }

        [Fact]
        public void A_blank_or_too_long_reference_is_rejected()
        {
            foreach (var reference in new string?[] { null, "", "   ", new string('r', StudentCharge.MaxReferenceLength + 1) })
            {
                var result = StudentCharge.CreateService(Guid.NewGuid(), Guid.NewGuid(), "Transcript", Amount(10m), Due, reference, Now);

                Assert.Equal(FinanceErrors.ServiceReferenceInvalid.Code, result.Error?.Code);
            }
        }

        [Fact]
        public void A_zero_amount_or_empty_ids_are_rejected()
        {
            Assert.Equal(FinanceErrors.AmountMustBePositive.Code, StudentCharge.CreateService(Guid.NewGuid(), Guid.NewGuid(), "x", Money.Zero, Due, "REQ-1", Now).Error?.Code);
            Assert.Equal(FinanceErrors.ChargeDataInvalid.Code, StudentCharge.CreateService(Guid.Empty, Guid.NewGuid(), "x", Amount(1m), Due, "REQ-1", Now).Error?.Code);
            Assert.Equal(FinanceErrors.ChargeDataInvalid.Code, StudentCharge.CreateService(Guid.NewGuid(), Guid.Empty, "x", Amount(1m), Due, "REQ-1", Now).Error?.Code);
        }

        [Fact]
        public void A_service_charge_can_be_invoiced_or_cancelled_like_any_charge()
        {
            var invoiced = NewService();
            Assert.True(invoiced.MarkInvoiced(Guid.NewGuid(), Now).IsSuccess);
            Assert.Equal(ChargeStatus.Invoiced, invoiced.Status);

            var cancelled = NewService();
            Assert.True(cancelled.Cancel(Now).IsSuccess);
            Assert.Equal(ChargeStatus.Cancelled, cancelled.Status);
        }
    }


}
