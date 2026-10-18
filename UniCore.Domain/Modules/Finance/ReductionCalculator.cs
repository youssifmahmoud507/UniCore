using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // The order of the reductions on an invoice:
    //  1. scholarships, then discounts;
    //  2. inside each group, percentages before fixed amounts;
    //  3. every percentage is taken from what is LEFT at that point, not from the subtotal;
    //  4. a reduction never takes more than what is left, so the total never goes below zero.
    public static class ReductionCalculator
    {
        public static Result<IReadOnlyList<AppliedReduction>> Apply(Money subtotal, IReadOnlyList<ReductionInput> inputs)
        {
            var ordered = inputs.OrderBy(input => (int)input.Kind).ThenBy(input => (int)input.Type).ThenBy(input => input.SourceId).ToList();

            var applied = new List<AppliedReduction>();
            var remaining = subtotal;

            foreach (var input in ordered)
            {
                if (!remaining.IsPositive)
                {
                    break;
                }

                var raw = input.Type == ReductionType.Percentage
                    ? remaining.Percent(input.Value)
                    : Money.Rounded(input.Value);

                if (!raw.TryGetValue(out var amount))
                {
                    return Result<IReadOnlyList<AppliedReduction>>.Fail(raw.Errors);
                }

                if (amount > remaining)
                {
                    amount = remaining;
                }

                if (!amount.IsPositive)
                {
                    continue;
                }

                var left = remaining.Subtract(amount);

                if (!left.TryGetValue(out var next))
                {
                    return Result<IReadOnlyList<AppliedReduction>>.Fail(left.Errors);
                }

                applied.Add(new AppliedReduction(input, amount));
                remaining = next;
            }

            return Result<IReadOnlyList<AppliedReduction>>.Ok(applied);
        }
    }



}
