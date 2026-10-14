using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // One kind of fee in the catalog (tuition, lab fee, ID card...). Prices live in FeeStructureItem.
    // UniversityId has no foreign key yet: University belongs to Developer 1.
    public sealed class FeeDefinition : AggregateRoot
    {
        public const int MinCodeLength = 2;
        public const int MaxCodeLength = 30;
        public const int MaxNameLength = 150;
        public const int MaxDescriptionLength = 500;

        private sealed record Details(string Name, string? Description);

        private FeeDefinition()
        {
        }

        public Guid UniversityId { get; private set; }
        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public string? Description { get; private set; }
        public Money DefaultAmount { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public static Result<FeeDefinition> Create(Guid universityId, string? code, string? name, string? description, Money defaultAmount, DateTimeOffset now)
        {
            if (universityId == Guid.Empty)
            {
                return FinanceErrors.FeeDefinitionDataInvalid;
            }

            var cleanCode = NormalizeCode(code);

            if (cleanCode is null)
            {
                return FinanceErrors.FeeCodeInvalid;
            }

            var details = Clean(name, description, defaultAmount);

            if (!details.TryGetValue(out var clean))
            {
                return Result<FeeDefinition>.Fail(details.Errors);
            }

            return new FeeDefinition
            {
                Id = Guid.NewGuid(),
                UniversityId = universityId,
                Code = cleanCode,
                Name = clean.Name,
                Description = clean.Description,
                DefaultAmount = defaultAmount,
                IsActive = true,
                CreatedAt = now
            };
        }

        // Codes are stored upper case: letters A-Z, digits and underscore only. Null means invalid.
        public static string? NormalizeCode(string? code)
        {
            var clean = code?.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(clean) || clean.Length < MinCodeLength || clean.Length > MaxCodeLength)
            {
                return null;
            }

            return clean.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_') ? clean : null;
        }

        // The code never changes after creation.
        public Result Update(string? name, string? description, Money defaultAmount, DateTimeOffset now)
        {
            var details = Clean(name, description, defaultAmount);

            if (!details.TryGetValue(out var clean))
            {
                return Result.Fail(details.Errors);
            }

            Name = clean.Name;
            Description = clean.Description;
            DefaultAmount = defaultAmount;
            UpdatedAt = now;

            return Result.Ok();
        }

        // Setting the state it already has is a no-op.
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

        private static Result<Details> Clean(string? name, string? description, Money amount)
        {
            var cleanName = name?.Trim();

            if (string.IsNullOrEmpty(cleanName) || cleanName.Length > MaxNameLength)
            {
                return FinanceErrors.FeeDefinitionDataInvalid;
            }

            var cleanDescription = description?.Trim();

            if (string.IsNullOrEmpty(cleanDescription))
            {
                cleanDescription = null;
            }
            else if (cleanDescription.Length > MaxDescriptionLength)
            {
                return FinanceErrors.FeeDefinitionDataInvalid;
            }

            if (!amount.IsPositive)
            {
                return FinanceErrors.AmountMustBePositive;
            }

            return new Details(cleanName, cleanDescription);
        }
    }


}
