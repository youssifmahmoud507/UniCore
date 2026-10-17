using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class ReductionCalculatorTests
    {
        private static Money Amount(decimal value)
        {
            Assert.True(Money.Of(value).TryGetValue(out var money));
            return money;
        }

        private static ReductionInput Input(ReductionKind kind, ReductionType type, decimal value, string name = "R")
            => new(kind, Guid.NewGuid(), name, type, value);

        private static IReadOnlyList<AppliedReduction> Apply(decimal subtotal, params ReductionInput[] inputs)
        {
            var result = ReductionCalculator.Apply(Amount(subtotal), inputs);
            Assert.True(result.TryGetValue(out var applied));
            Assert.NotNull(applied);
            return applied;
        }

        [Fact]
        public void No_reductions_means_nothing_is_taken_off()
        {
            Assert.Empty(Apply(1000m));
        }

        [Fact]
        public void Scholarships_come_before_discounts_and_percentages_before_fixed_amounts()
        {
            // Given in a scrambled order on purpose.
            var applied = Apply(1000m,
                Input(ReductionKind.Discount, ReductionType.FixedAmount, 20m, "D fixed"),
                Input(ReductionKind.Scholarship, ReductionType.FixedAmount, 100m, "S fixed"),
                Input(ReductionKind.Discount, ReductionType.Percentage, 10m, "D pct"),
                Input(ReductionKind.Scholarship, ReductionType.Percentage, 50m, "S pct"));

            Assert.Equal(["S pct", "S fixed", "D pct", "D fixed"], applied.Select(a => a.Input.Name).ToArray());

            // 50% of 1000 = 500 (500 left); 100 off (400 left); 10% of 400 = 40 (360 left); 20 off (340 left).
            Assert.Equal([500m, 100m, 40m, 20m], applied.Select(a => a.Amount.Amount).ToArray());
        }

        [Fact]
        public void A_reduction_never_takes_more_than_what_is_left_and_later_ones_are_skipped()
        {
            var applied = Apply(100m,
                Input(ReductionKind.Scholarship, ReductionType.FixedAmount, 500m, "S"),
                Input(ReductionKind.Discount, ReductionType.Percentage, 10m, "D"));

            var only = Assert.Single(applied);
            Assert.Equal("S", only.Input.Name);
            Assert.Equal(100m, only.Amount.Amount);
        }

        [Fact]
        public void A_full_scholarship_takes_everything()
        {
            var applied = Apply(750.50m, Input(ReductionKind.Scholarship, ReductionType.Percentage, 100m));

            Assert.Equal(750.50m, Assert.Single(applied).Amount.Amount);
        }

        [Fact]
        public void A_percentage_is_rounded_to_two_decimals()
        {
            var applied = Apply(333.33m, Input(ReductionKind.Discount, ReductionType.Percentage, 10m));

            Assert.Equal(33.33m, Assert.Single(applied).Amount.Amount);
        }

        [Fact]
        public void A_zero_subtotal_gets_no_reductions()
        {
            Assert.Empty(Apply(0m, Input(ReductionKind.Scholarship, ReductionType.Percentage, 50m)));
        }
    }



}
