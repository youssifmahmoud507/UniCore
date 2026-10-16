using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.Calendar
{
    // A kind of semester that repeats every year: Fall, Spring, Summer. A semester is one term inside one year.
    public sealed class AcademicTerm : AggregateRoot
    {
        public const int MaxSortOrder = 10;

        private AcademicTerm()
        {
        }

        // Unique (database index). Upper-case, never changed.
        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public int SortOrder { get; private set; }
        public bool IsActive { get; private set; }

        public static Result<AcademicTerm> Create(string? code, string? name, int sortOrder)
        {
            var cleanCode = code?.Trim().ToUpperInvariant();
            if (cleanCode is null || !FieldRules.IsValidCode(cleanCode, FieldRules.CodeMinLength, FieldRules.CodeMaxLength))
            {
                return CalendarErrors.InvalidCode;
            }

            var term = new AcademicTerm { Id = Guid.NewGuid(), Code = cleanCode, IsActive = true };

            var details = term.ApplyDetails(name, sortOrder);
            if (details.IsFailure)
            {
                return Result<AcademicTerm>.Fail(details.Errors);
            }

            return term;
        }

        public Result Update(string? name, int sortOrder) => ApplyDetails(name, sortOrder);

        public Result Activate()
        {
            if (IsActive)
            {
                return Result.Fail(CalendarErrors.AlreadyActive);
            }

            IsActive = true;
            return Result.Ok();
        }

        public Result Deactivate()
        {
            if (!IsActive)
            {
                return Result.Fail(CalendarErrors.AlreadyInactive);
            }

            IsActive = false;
            return Result.Ok();
        }

        private Result ApplyDetails(string? name, int sortOrder)
        {
            var cleanName = name?.Trim();
            if (cleanName is null || cleanName.Length < FieldRules.NameMinLength || cleanName.Length > FieldRules.NameMaxLength)
            {
                return Result.Fail(CalendarErrors.InvalidName);
            }

            if (sortOrder < 1 || sortOrder > MaxSortOrder)
            {
                return Result.Fail(CalendarErrors.InvalidTermOrder);
            }

            Name = cleanName;
            SortOrder = sortOrder;
            return Result.Ok();
        }
    }
}
