using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // The definition of a scholarship (merit, need-based...). Who holds it, and for how long, is a StudentScholarship.
    public sealed class Scholarship : AggregateRoot
    {
        private sealed record Checked(string Name, string? Description, decimal Value);

        private Scholarship()
        {
        }

        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public string? Description { get; private set; }
        public ReductionType Type { get; private set; }
        public decimal Value { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public static Result<Scholarship> Create(string? code, string? name, string? description, ReductionType type, decimal value, DateTimeOffset now)
        {
            var cleanCode = FeeDefinition.NormalizeCode(code);

            if (cleanCode is null)
            {
                return FinanceErrors.ReductionCodeInvalid;
            }

            var checkedData = Validate(name, description, type, value);

            if (!checkedData.TryGetValue(out var data))
            {
                return Result<Scholarship>.Fail(checkedData.Errors);
            }

            return new Scholarship
            {
                Id = Guid.NewGuid(),
                Code = cleanCode,
                Name = data.Name,
                Description = data.Description,
                Type = type,
                Value = data.Value,
                IsActive = true,
                CreatedAt = now
            };
        }

        // The code never changes. A new value applies to future invoices only: issued invoices keep their own copy.
        public Result Update(string? name, string? description, ReductionType type, decimal value, DateTimeOffset now)
        {
            var checkedData = Validate(name, description, type, value);

            if (!checkedData.TryGetValue(out var data))
            {
                return Result.Fail(checkedData.Errors);
            }

            Name = data.Name;
            Description = data.Description;
            Type = type;
            Value = data.Value;
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

        private static Result<Checked> Validate(string? name, string? description, ReductionType type, decimal value)
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

            return new Checked(clean.Name, clean.Description, amount);
        }
    }







}
