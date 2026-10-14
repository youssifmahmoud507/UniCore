using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class MoneyTests
    {
        private static Money Amount(decimal value)
        {
            var result = Money.Of(value);
            Assert.True(result.TryGetValue(out var money));
            return money;
        }

        [Fact]
        public void A_typed_in_amount_with_two_decimals_is_accepted()
        {
            Assert.Equal(1500.50m, Amount(1500.50m).Amount);
            Assert.Equal("1500.50", Amount(1500.5m).ToString());
        }

        [Theory]
        [InlineData(10.001)]
        [InlineData(0.005)]
        public void An_amount_with_more_than_two_decimals_is_rejected_not_rounded(double value)
        {
            var result = Money.Of((decimal)value);

            Assert.True(result.IsFailure);
            Assert.Equal("FINANCE_AMOUNT_PRECISION_INVALID", result.Errors[0].Code);
        }

        [Fact]
        public void An_amount_beyond_the_limit_is_rejected()
        {
            var over = Money.Of(Money.MaxAbsoluteAmount + 1m);
            var edge = Money.Of(Money.MaxAbsoluteAmount);

            Assert.Equal("FINANCE_AMOUNT_OUT_OF_RANGE", over.Errors[0].Code);
            Assert.True(edge.IsSuccess);
        }

        [Fact]
        public void The_default_money_is_zero()
        {
            Assert.Equal(Money.Zero, default(Money));
            Assert.True(Money.Zero.IsZero);
            Assert.False(Money.Zero.IsPositive);
            Assert.False(Money.Zero.IsNegative);
        }

        [Fact]
        public void A_computed_amount_is_rounded_half_away_from_zero()
        {
            Assert.Equal(2.35m, Money.Rounded(2.345m).Value.Amount);
            Assert.Equal(-2.35m, Money.Rounded(-2.345m).Value.Amount);
            Assert.Equal(2.34m, Money.Rounded(2.344m).Value.Amount);
        }

        [Fact]
        public void Adding_and_subtracting_keep_two_decimals()
        {
            Assert.Equal(300.75m, Amount(100.25m).Add(Amount(200.50m)).Value.Amount);
            Assert.Equal(-100.25m, Amount(100.25m).Subtract(Amount(200.50m)).Value.Amount);
            Assert.True(Amount(5m).Subtract(Amount(10m)).Value.IsNegative);
        }

        [Fact]
        public void Multiplying_by_a_quantity_works_and_overflowing_the_limit_is_an_error()
        {
            Assert.Equal(75.00m, Amount(25m).Multiply(3).Value.Amount);

            var overflow = Amount(Money.MaxAbsoluteAmount).Multiply(2);
            Assert.Equal("FINANCE_AMOUNT_OUT_OF_RANGE", overflow.Errors[0].Code);
        }

        [Fact]
        public void A_percentage_is_rounded_and_must_be_between_0_and_100()
        {
            Assert.Equal(123.46m, Amount(1234.55m).Percent(10m).Value.Amount);
            Assert.Equal("FINANCE_PERCENT_INVALID", Amount(100m).Percent(100.01m).Errors[0].Code);
            Assert.Equal("FINANCE_PERCENT_INVALID", Amount(100m).Percent(-1m).Errors[0].Code);
        }

        [Fact]
        public void Amounts_compare_by_value()
        {
            Assert.Equal(Amount(10m), Amount(10.00m));
            Assert.True(Amount(10m) < Amount(11m));
            Assert.True(Amount(11m) >= Amount(11m));
            Assert.True(Amount(5m).CompareTo(Amount(9m)) < 0);
        }
    }

}
