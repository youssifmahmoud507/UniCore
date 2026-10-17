using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class FinancialHoldRulesTests
    {
        private static readonly DateOnly Today = new(2026, 10, 17);

        private static Money Amount(decimal value)
        {
            Assert.True(Money.Of(value).TryGetValue(out var money));
            return money;
        }

        [Fact]
        public void The_cutoff_is_today_minus_the_grace_days()
        {
            Assert.Equal(new DateOnly(2026, 10, 3), FinancialHoldRules.OverdueCutoff(Today, 14));
            Assert.Equal(Today, FinancialHoldRules.OverdueCutoff(Today, 0));
        }

        [Fact]
        public void An_invoice_is_overdue_only_after_its_due_date_plus_the_grace_days()
        {
            // Due 14 days ago with 14 grace days: not overdue yet (today is the last day of grace).
            Assert.False(FinancialHoldRules.IsOverdue(new DateOnly(2026, 10, 3), Today, 14));

            // One day later it is.
            Assert.True(FinancialHoldRules.IsOverdue(new DateOnly(2026, 10, 2), Today, 14));

            Assert.False(FinancialHoldRules.IsOverdue(Today, Today, 0));
            Assert.True(FinancialHoldRules.IsOverdue(Today.AddDays(-1), Today, 0));
            Assert.False(FinancialHoldRules.IsOverdue(Today.AddDays(30), Today, 0));
        }

        [Fact]
        public void A_student_is_held_only_when_the_overdue_total_reaches_the_minimum()
        {
            var minimum = Amount(100m);

            Assert.True(FinancialHoldRules.ShouldHold(Amount(100m), minimum));
            Assert.True(FinancialHoldRules.ShouldHold(Amount(100.01m), minimum));
            Assert.False(FinancialHoldRules.ShouldHold(Amount(99.99m), minimum));
            Assert.False(FinancialHoldRules.ShouldHold(Money.Zero, minimum));
        }

        [Fact]
        public void Nothing_overdue_is_never_held_even_with_a_zero_minimum()
        {
            Assert.False(FinancialHoldRules.ShouldHold(Money.Zero, Money.Zero));
            Assert.True(FinancialHoldRules.ShouldHold(Amount(0.01m), Money.Zero));
        }
    }


}
