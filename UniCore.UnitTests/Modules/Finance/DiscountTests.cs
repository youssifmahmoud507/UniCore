using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class DiscountTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Today = new(2026, 10, 17);

        private static Discount NewDiscount(ReductionType type = ReductionType.Percentage, decimal value = 10m, DateOnly? from = null, DateOnly? to = null)
        {
            var result = Discount.Create(" early_bird ", "Early bird", "Pay early", type, value, from, to, Now);
            Assert.True(result.TryGetValue(out var discount));
            Assert.NotNull(discount);
            return discount;
        }

        [Fact]
        public void A_new_discount_is_active_with_a_normalized_code()
        {
            var discount = NewDiscount();

            Assert.NotEqual(Guid.Empty, discount.Id);
            Assert.Equal("EARLY_BIRD", discount.Code);
            Assert.Equal("Early bird", discount.Name);
            Assert.Equal(ReductionType.Percentage, discount.Type);
            Assert.Equal(10m, discount.Value);
            Assert.True(discount.IsActive);
            Assert.Equal(Now, discount.CreatedAt);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("A")]
        [InlineData("has space")]
        public void A_bad_code_is_rejected(string? code)
        {
            var result = Discount.Create(code, "Name", null, ReductionType.Percentage, 10m, null, null, Now);

            Assert.Equal("REDUCTION_CODE_INVALID", result.Errors[0].Code);
        }

        [Fact]
        public void A_bad_name_or_description_or_type_is_rejected()
        {
            Assert.Equal("REDUCTION_DATA_INVALID", Discount.Create("D1", "  ", null, ReductionType.Percentage, 10m, null, null, Now).Errors[0].Code);
            Assert.Equal("REDUCTION_DATA_INVALID", Discount.Create("D1", new string('N', 151), null, ReductionType.Percentage, 10m, null, null, Now).Errors[0].Code);
            Assert.Equal("REDUCTION_DATA_INVALID", Discount.Create("D1", "Name", new string('D', 501), ReductionType.Percentage, 10m, null, null, Now).Errors[0].Code);
            Assert.Equal("REDUCTION_DATA_INVALID", Discount.Create("D1", "Name", null, (ReductionType)99, 10m, null, null, Now).Errors[0].Code);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(100.01)]
        public void A_percentage_must_be_above_zero_and_at_most_100(double value)
        {
            var result = Discount.Create("D1", "Name", null, ReductionType.Percentage, (decimal)value, null, null, Now);

            Assert.Equal("FINANCE_PERCENT_INVALID", result.Errors[0].Code);
        }

        [Fact]
        public void A_percentage_has_at_most_two_decimals_and_100_is_allowed()
        {
            Assert.Equal("FINANCE_AMOUNT_PRECISION_INVALID", Discount.Create("D1", "Name", null, ReductionType.Percentage, 10.123m, null, null, Now).Errors[0].Code);
            Assert.True(Discount.Create("D1", "Name", null, ReductionType.Percentage, 100m, null, null, Now).IsSuccess);
            Assert.True(Discount.Create("D1", "Name", null, ReductionType.Percentage, 12.5m, null, null, Now).IsSuccess);
        }

        [Fact]
        public void A_fixed_amount_must_be_positive_with_two_decimals()
        {
            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", Discount.Create("D1", "Name", null, ReductionType.FixedAmount, 0m, null, null, Now).Errors[0].Code);
            Assert.Equal("FINANCE_AMOUNT_PRECISION_INVALID", Discount.Create("D1", "Name", null, ReductionType.FixedAmount, 10.123m, null, null, Now).Errors[0].Code);
            Assert.True(Discount.Create("D1", "Name", null, ReductionType.FixedAmount, 250.50m, null, null, Now).IsSuccess);
        }

        [Fact]
        public void The_end_date_cannot_be_before_the_start_date()
        {
            var result = Discount.Create("D1", "Name", null, ReductionType.Percentage, 10m, Today, Today.AddDays(-1), Now);

            Assert.Equal("REDUCTION_PERIOD_INVALID", result.Errors[0].Code);
        }

        [Fact]
        public void A_discount_is_valid_only_inside_its_window_and_while_active()
        {
            var open = NewDiscount();
            var windowed = NewDiscount(from: Today, to: Today.AddDays(10));

            Assert.True(open.IsValidOn(Today));
            Assert.True(windowed.IsValidOn(Today));
            Assert.True(windowed.IsValidOn(Today.AddDays(10)));
            Assert.False(windowed.IsValidOn(Today.AddDays(-1)));
            Assert.False(windowed.IsValidOn(Today.AddDays(11)));

            open.SetActive(false, Now.AddDays(1));
            Assert.False(open.IsValidOn(Today));
        }

        [Fact]
        public void Update_changes_the_details_but_not_the_code_and_a_failed_update_changes_nothing()
        {
            var discount = NewDiscount();

            Assert.True(discount.Update("New name", null, ReductionType.FixedAmount, 75.25m, Today, null, Now.AddDays(1)).IsSuccess);
            Assert.Equal("EARLY_BIRD", discount.Code);
            Assert.Equal("New name", discount.Name);
            Assert.Equal(ReductionType.FixedAmount, discount.Type);
            Assert.Equal(75.25m, discount.Value);
            Assert.Equal(Now.AddDays(1), discount.UpdatedAt);

            Assert.True(discount.Update("", null, ReductionType.Percentage, 5m, null, null, Now.AddDays(2)).IsFailure);
            Assert.Equal("New name", discount.Name);
            Assert.Equal(75.25m, discount.Value);
        }

        [Fact]
        public void Setting_the_state_it_already_has_does_nothing()
        {
            var discount = NewDiscount();

            Assert.True(discount.SetActive(true, Now.AddDays(1)).IsSuccess);
            Assert.Null(discount.UpdatedAt);

            Assert.True(discount.SetActive(false, Now.AddDays(2)).IsSuccess);
            Assert.False(discount.IsActive);
            Assert.Equal(Now.AddDays(2), discount.UpdatedAt);
        }
    }


}
