using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // The price list for a program and/or a semester. Null means "all".
    // Draft: items can change. Active: frozen. Archived: history. Only one Active structure per
    // (program, semester) pair: the handler checks it and a unique index enforces it.
    public sealed class FeeStructure : AggregateRoot
    {
        public const int MaxNameLength = 150;
        public const int MaxItems = 50;

        private readonly List<FeeStructureItem> _items = [];

        private FeeStructure()
        {
        }

        public Guid? AcademicProgramId { get; private set; }
        public Guid? AcademicSemesterId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public FeeStructureStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }
        public DateTimeOffset? ActivatedAt { get; private set; }
        public DateTimeOffset? ArchivedAt { get; private set; }

        public IReadOnlyList<FeeStructureItem> Items => _items.AsReadOnly();

        // The sum of valid amounts, so it cannot go out of range (at most MaxItems items).
        public Money Total => Money.FromStored(_items.Sum(item => item.Amount.Amount));

        public static Result<FeeStructure> Create(Guid? academicProgramId, Guid? academicSemesterId, string? name, DateTimeOffset now)
        {
            if (academicProgramId == Guid.Empty || academicSemesterId == Guid.Empty)
            {
                return FinanceErrors.FeeStructureDataInvalid;
            }

            var cleanName = name?.Trim();

            if (string.IsNullOrEmpty(cleanName) || cleanName.Length > MaxNameLength)
            {
                return FinanceErrors.FeeStructureDataInvalid;
            }

            return new FeeStructure
            {
                Id = Guid.NewGuid(),
                AcademicProgramId = academicProgramId,
                AcademicSemesterId = academicSemesterId,
                Name = cleanName,
                Status = FeeStructureStatus.Draft,
                CreatedAt = now
            };
        }

        public Result<FeeStructureItem> AddItem(Guid feeDefinitionId, Money amount, DateTimeOffset now)
        {
            if (Status != FeeStructureStatus.Draft)
            {
                return FinanceErrors.FeeStructureNotDraft;
            }

            if (feeDefinitionId == Guid.Empty)
            {
                return FinanceErrors.FeeStructureDataInvalid;
            }

            if (!amount.IsPositive)
            {
                return FinanceErrors.AmountMustBePositive;
            }

            if (_items.Count >= MaxItems)
            {
                return FinanceErrors.FeeStructureTooManyItems;
            }

            if (_items.Any(item => item.FeeDefinitionId == feeDefinitionId))
            {
                return FinanceErrors.FeeItemDuplicate;
            }

            var created = FeeStructureItem.Create(Id, feeDefinitionId, amount);
            _items.Add(created);
            UpdatedAt = now;

            return created;
        }

        public Result ChangeItemAmount(Guid itemId, Money amount, DateTimeOffset now)
        {
            if (Status != FeeStructureStatus.Draft)
            {
                return Result.Fail(FinanceErrors.FeeStructureNotDraft);
            }

            var existing = _items.FirstOrDefault(item => item.Id == itemId);

            if (existing is null)
            {
                return Result.Fail(FinanceErrors.FeeItemNotFound);
            }

            if (!amount.IsPositive)
            {
                return Result.Fail(FinanceErrors.AmountMustBePositive);
            }

            existing.SetAmount(amount);
            UpdatedAt = now;

            return Result.Ok();
        }

        public Result RemoveItem(Guid itemId, DateTimeOffset now)
        {
            if (Status != FeeStructureStatus.Draft)
            {
                return Result.Fail(FinanceErrors.FeeStructureNotDraft);
            }

            var existing = _items.FirstOrDefault(item => item.Id == itemId);

            if (existing is null)
            {
                return Result.Fail(FinanceErrors.FeeItemNotFound);
            }

            _items.Remove(existing);
            UpdatedAt = now;

            return Result.Ok();
        }

        public Result Activate(DateTimeOffset now)
        {
            if (Status != FeeStructureStatus.Draft)
            {
                return Result.Fail(FinanceErrors.FeeStructureNotDraft);
            }

            if (_items.Count == 0)
            {
                return Result.Fail(FinanceErrors.FeeStructureEmpty);
            }

            Status = FeeStructureStatus.Active;
            ActivatedAt = now;
            UpdatedAt = now;

            return Result.Ok();
        }

        public Result Archive(DateTimeOffset now)
        {
            if (Status != FeeStructureStatus.Active)
            {
                return Result.Fail(FinanceErrors.FeeStructureNotActive);
            }

            Status = FeeStructureStatus.Archived;
            ArchivedAt = now;
            UpdatedAt = now;

            return Result.Ok();
        }
    }


}
