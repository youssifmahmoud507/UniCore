using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // The checks that Discount and Scholarship share.
    internal static class ReductionRules
    {
        public const int MaxNameLength = 150;
        public const int MaxDescriptionLength = 500;

        internal sealed record Texts(string Name, string? Description);

        public static Result<Texts> CleanTexts(string? name, string? description)
        {
            var cleanName = name?.Trim();

            if (string.IsNullOrEmpty(cleanName) || cleanName.Length > MaxNameLength)
            {
                return FinanceErrors.ReductionDataInvalid;
            }

            var cleanDescription = description?.Trim();

            if (string.IsNullOrEmpty(cleanDescription))
            {
                cleanDescription = null;
            }
            else if (cleanDescription.Length > MaxDescriptionLength)
            {
                return FinanceErrors.ReductionDataInvalid;
            }

            return new Texts(cleanName, cleanDescription);
        }

        // A percentage is above 0 and at most 100, with at most 2 decimals. A fixed amount is a positive Money.
        public static Result<decimal> CleanValue(ReductionType type, decimal value)
        {
            if (!Enum.IsDefined(type))
            {
                return FinanceErrors.ReductionDataInvalid;
            }

            if (type == ReductionType.Percentage)
            {
                if (value <= 0m || value > 100m)
                {
                    return FinanceErrors.PercentInvalid;
                }

                if (Math.Round(value, 2, MidpointRounding.AwayFromZero) != value)
                {
                    return FinanceErrors.AmountPrecisionInvalid;
                }

                return value;
            }

            var money = Money.Of(value);

            if (!money.TryGetValue(out var amount))
            {
                return Result<decimal>.Fail(money.Errors);
            }

            if (!amount.IsPositive)
            {
                return FinanceErrors.AmountMustBePositive;
            }

            return amount.Amount;
        }
    }

}
