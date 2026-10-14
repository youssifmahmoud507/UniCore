using System.Globalization;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // An amount of money: one currency, 2 decimals. default(Money) is zero.
    // Rounding rule: half away from zero. Typed-in amounts are never rounded silently (Of);
    // computed amounts are rounded (Rounded, Percent, Multiply).
    public readonly record struct Money : IComparable<Money>
    {
        public const int Scale = 2;
        public const decimal MaxAbsoluteAmount = 1_000_000_000_000m;

        private Money(decimal amount)
        {
            Amount = amount;
        }

        public decimal Amount { get; }

        public static Money Zero => default;

        public bool IsZero => Amount == 0m;

        public bool IsPositive => Amount > 0m;

        public bool IsNegative => Amount < 0m;

        // For amounts that come from a person or a client: at most 2 decimals, within range.
        public static Result<Money> Of(decimal amount)
        {
            if (Math.Abs(amount) > MaxAbsoluteAmount)
            {
                return FinanceErrors.AmountOutOfRange;
            }

            var rounded = Math.Round(amount, Scale, MidpointRounding.AwayFromZero);

            if (rounded != amount)
            {
                return FinanceErrors.AmountPrecisionInvalid;
            }

            return new Money(rounded);
        }

        // For computed amounts (percentages, shares): rounded to 2 decimals.
        public static Result<Money> Rounded(decimal amount)
        {
            if (Math.Abs(amount) > MaxAbsoluteAmount)
            {
                return FinanceErrors.AmountOutOfRange;
            }

            return new Money(Math.Round(amount, Scale, MidpointRounding.AwayFromZero));
        }

        // For values read back from the database (already validated when they were saved)
        // and for sums of amounts that are already valid.
        public static Money FromStored(decimal amount) => new(amount);

        public Result<Money> Add(Money other) => Rounded(Amount + other.Amount);

        public Result<Money> Subtract(Money other) => Rounded(Amount - other.Amount);

        public Result<Money> Multiply(int quantity) => Rounded(Amount * quantity);

        public Result<Money> Percent(decimal percent)
        {
            if (percent < 0m || percent > 100m)
            {
                return FinanceErrors.PercentInvalid;
            }

            return Rounded(Amount * percent / 100m);
        }

        public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

        public static bool operator <(Money left, Money right) => left.Amount < right.Amount;

        public static bool operator >(Money left, Money right) => left.Amount > right.Amount;

        public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

        public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;

        public override string ToString() => Amount.ToString("0.00", CultureInfo.InvariantCulture);
    }


}
