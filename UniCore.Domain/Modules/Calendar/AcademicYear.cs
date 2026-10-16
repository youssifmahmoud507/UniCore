using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.Calendar
{
    // One academic year, for example "2026/2027". Planned -> Active -> Closed. A closed year is read-only.
    // Exactly one year is the current one (IsCurrent); the database enforces it with a filtered unique index.
    public sealed class AcademicYear : AggregateRoot
    {
        private AcademicYear()
        {
        }

        public string Name { get; private set; } = string.Empty;
        public DateOnly StartDate { get; private set; }
        public DateOnly EndDate { get; private set; }
        public AcademicYearStatus Status { get; private set; }
        public bool IsCurrent { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // The application layer checks that no other year overlaps these dates.
        public static Result<AcademicYear> Create(string? name, DateOnly startDate, DateOnly endDate, DateTimeOffset now)
        {
            var year = new AcademicYear
            {
                Id = Guid.NewGuid(),
                Status = AcademicYearStatus.Planned,
                CreatedAt = now
            };

            var details = year.ApplyDetails(name, startDate, endDate);
            if (details.IsFailure)
            {
                return Result<AcademicYear>.Fail(details.Errors);
            }

            return year;
        }

        public Result UpdateDetails(string? name, DateOnly startDate, DateOnly endDate, DateTimeOffset now)
        {
            if (Status == AcademicYearStatus.Closed)
            {
                return Result.Fail(CalendarErrors.AcademicYearClosed);
            }

            var details = ApplyDetails(name, startDate, endDate);
            if (details.IsFailure)
            {
                return details;
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Activate(DateTimeOffset now)
        {
            if (Status != AcademicYearStatus.Planned)
            {
                return Result.Fail(CalendarErrors.AcademicYearInvalidState);
            }

            Status = AcademicYearStatus.Active;
            UpdatedAt = now;
            return Result.Ok();
        }

        // Only an active year can be the current one. The caller looks up whether another year already is.
        public Result MakeCurrent(bool anotherYearIsCurrent, DateTimeOffset now)
        {
            if (Status != AcademicYearStatus.Active || IsCurrent)
            {
                return Result.Fail(CalendarErrors.AcademicYearInvalidState);
            }

            if (anotherYearIsCurrent)
            {
                return Result.Fail(CalendarErrors.AcademicYearCurrentExists);
            }

            IsCurrent = true;
            UpdatedAt = now;
            return Result.Ok();
        }

        // The caller looks up whether the year still has semesters that are not closed.
        public Result Close(bool hasOpenSemesters, DateTimeOffset now)
        {
            if (Status != AcademicYearStatus.Active)
            {
                return Result.Fail(CalendarErrors.AcademicYearInvalidState);
            }

            if (hasOpenSemesters)
            {
                return Result.Fail(CalendarErrors.AcademicYearHasOpenSemesters);
            }

            Status = AcademicYearStatus.Closed;
            IsCurrent = false;
            UpdatedAt = now;
            return Result.Ok();
        }

        public bool Contains(DateOnly start, DateOnly end) => start >= StartDate && end <= EndDate;

        public bool Overlaps(DateOnly start, DateOnly end) => start <= EndDate && end >= StartDate;

        private Result ApplyDetails(string? name, DateOnly startDate, DateOnly endDate)
        {
            var cleanName = name?.Trim();
            if (cleanName is null || cleanName.Length < FieldRules.NameMinLength || cleanName.Length > FieldRules.NameMaxLength)
            {
                return Result.Fail(CalendarErrors.InvalidName);
            }

            if (startDate >= endDate)
            {
                return Result.Fail(CalendarErrors.AcademicYearInvalidDates);
            }

            Name = cleanName;
            StartDate = startDate;
            EndDate = endDate;
            return Result.Ok();
        }
    }
}
