using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.Calendar
{
    // A level of a program (for example "Level 2"). Order is unique inside the program (database index).
    // RequiredCredits is the earned credits needed to reach the level; gpa-rules.md uses it to compute a student's level.
    public sealed class AcademicLevel : AggregateRoot
    {
        public const int MaxOrder = 20;
        public const int MaxRequiredCredits = 400;

        private AcademicLevel()
        {
        }

        public Guid AcademicProgramId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public int Order { get; private set; }
        public int RequiredCredits { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // The application layer checks that the program exists and is active.
        public static Result<AcademicLevel> Create(Guid academicProgramId, string? name, int order, int requiredCredits, DateTimeOffset now)
        {
            if (academicProgramId == Guid.Empty)
            {
                return CalendarErrors.InvalidParent;
            }

            var level = new AcademicLevel { Id = Guid.NewGuid(), AcademicProgramId = academicProgramId, IsActive = true, CreatedAt = now };

            var details = level.ApplyDetails(name, order, requiredCredits);
            if (details.IsFailure)
            {
                return Result<AcademicLevel>.Fail(details.Errors);
            }

            return level;
        }

        public Result Update(string? name, int order, int requiredCredits, DateTimeOffset now)
        {
            var details = ApplyDetails(name, order, requiredCredits);
            if (details.IsFailure)
            {
                return details;
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Activate(DateTimeOffset now)
        {
            if (IsActive)
            {
                return Result.Fail(CalendarErrors.AlreadyActive);
            }

            IsActive = true;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Deactivate(DateTimeOffset now)
        {
            if (!IsActive)
            {
                return Result.Fail(CalendarErrors.AlreadyInactive);
            }

            IsActive = false;
            UpdatedAt = now;
            return Result.Ok();
        }

        private Result ApplyDetails(string? name, int order, int requiredCredits)
        {
            var cleanName = name?.Trim();
            if (cleanName is null || cleanName.Length < FieldRules.NameMinLength || cleanName.Length > FieldRules.NameMaxLength)
            {
                return Result.Fail(CalendarErrors.InvalidName);
            }

            if (order < 1 || order > MaxOrder)
            {
                return Result.Fail(CalendarErrors.InvalidLevelOrder);
            }

            if (requiredCredits < 0 || requiredCredits > MaxRequiredCredits)
            {
                return Result.Fail(CalendarErrors.InvalidLevelCredits);
            }

            Name = cleanName;
            Order = order;
            RequiredCredits = requiredCredits;
            return Result.Ok();
        }
    }
}
