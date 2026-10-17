using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // A discount staff can apply when an invoice is issued (early payment, sibling...).
    // Value is a percentage (0.01 to 100) or a fixed amount, depending on Type.
    public sealed class Discount : AggregateRoot
    {
        private sealed record Checked(string Name, string? Description, decimal Value);

        private Discount()
        {
        }

        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public string? Description { get; private set; }
        public ReductionType Type { get; private set; }
        public decimal Value { get; private set; }
        public DateOnly? ValidFrom { get; private set; }
        public DateOnly? ValidTo { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public static Result<Discount> Create(string? code, string? name, string? description, ReductionType type, decimal value, DateOnly? validFrom, DateOnly? validTo, DateTimeOffset now)
        {
            var cleanCode = FeeDefinition.NormalizeCode(code);

            if (cleanCode is null)
            {
                return FinanceErrors.ReductionCodeInvalid;
            }

            var checkedData = Validate(name, description, type, value, validFrom, validTo);

            if (!checkedData.TryGetValue(out var data))
            {
                return Result<Discount>.Fail(checkedData.Errors);
            }

            return new Discount
            {
                Id = Guid.NewGuid(),
                Code = cleanCode,
                Name = data.Name,
                Description = data.Description,
                Type = type,
                Value = data.Value,
                ValidFrom = validFrom,
                ValidTo = validTo,
                IsActive = true,
                CreatedAt = now
            };
        }

        // The code never changes.
        public Result Update(string? name, string? description, ReductionType type, decimal value, DateOnly? validFrom, DateOnly? validTo, DateTimeOffset now)
        {
            var checkedData = Validate(name, description, type, value, validFrom, validTo);

            if (!checkedData.TryGetValue(out var data))
            {
                return Result.Fail(checkedData.Errors);
            }

            Name = data.Name;
            Description = data.Description;
            Type = type;
            Value = data.Value;
            ValidFrom = validFrom;
            ValidTo = validTo;
            UpdatedAt = now;

            return Result.Ok();
        }

        public Result SetActive(bool active, DateTimeOffset now)
        {
            if (IsActive == active)
            {
                return Result.Ok();
            }

            IsActive = active;
            UpdatedAt = now;

            return Result.Ok();
        }

        public bool IsValidOn(DateOnly date)
            => IsActive && (ValidFrom is null || ValidFrom <= date) && (ValidTo is null || ValidTo >= date);

        private static Result<Checked> Validate(string? name, string? description, ReductionType type, decimal value, DateOnly? validFrom, DateOnly? validTo)
        {
            var texts = ReductionRules.CleanTexts(name, description);

            if (!texts.TryGetValue(out var clean))
            {
                return Result<Checked>.Fail(texts.Errors);
            }

            var cleanValue = ReductionRules.CleanValue(type, value);

            if (!cleanValue.TryGetValue(out var amount))
            {
                return Result<Checked>.Fail(cleanValue.Errors);
            }

            if (validFrom is { } from && validTo is { } to && to < from)
            {
                return FinanceErrors.ReductionPeriodInvalid;
            }

            return new Checked(clean.Name, clean.Description, amount);
        }
    }


}
