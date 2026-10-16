using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Calendar
{
    // One term of one academic year, with its date windows. (AcademicYearId, AcademicTermId) is unique.
    // The window checks (IsRegistrationOpen ...) take "today" as a parameter: the caller converts the clock
    // to the university time zone, so nothing here reads DateTime.Now.
    public sealed class AcademicSemester : AggregateRoot
    {
        private AcademicSemester()
        {
        }

        public Guid AcademicYearId { get; private set; }
        public Guid AcademicTermId { get; private set; }
        public DateOnly StartDate { get; private set; }
        public DateOnly EndDate { get; private set; }
        public SemesterStatus Status { get; private set; }

        public DateOnly? RegistrationStart { get; private set; }
        public DateOnly? RegistrationEnd { get; private set; }
        public DateOnly? AddDropStart { get; private set; }
        public DateOnly? AddDropEnd { get; private set; }
        public DateOnly? WithdrawalDeadline { get; private set; }
        public DateOnly? MidtermStart { get; private set; }
        public DateOnly? MidtermEnd { get; private set; }
        public DateOnly? FinalExamStart { get; private set; }
        public DateOnly? FinalExamEnd { get; private set; }
        public DateOnly? GradeSubmissionDeadline { get; private set; }
        public DateOnly? GradePublicationDate { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public SemesterWindows Windows => new(
            RegistrationStart, RegistrationEnd, AddDropStart, AddDropEnd, WithdrawalDeadline,
            MidtermStart, MidtermEnd, FinalExamStart, FinalExamEnd, GradeSubmissionDeadline, GradePublicationDate);

        // The application layer loads the year and term, checks the term is active and the year is not closed,
        // and checks the semester does not overlap another semester of the year. It passes the year's dates here.
        public static Result<AcademicSemester> Create(
            Guid academicYearId, Guid academicTermId, DateOnly startDate, DateOnly endDate, SemesterWindows? windows,
            DateOnly yearStart, DateOnly yearEnd, DateTimeOffset now)
        {
            if (academicYearId == Guid.Empty || academicTermId == Guid.Empty)
            {
                return CalendarErrors.InvalidParent;
            }

            var semester = new AcademicSemester
            {
                Id = Guid.NewGuid(),
                AcademicYearId = academicYearId,
                AcademicTermId = academicTermId,
                Status = SemesterStatus.Planned,
                CreatedAt = now
            };

            var applied = semester.ApplySchedule(startDate, endDate, windows ?? SemesterWindows.None, yearStart, yearEnd);
            if (applied.IsFailure)
            {
                return Result<AcademicSemester>.Fail(applied.Errors);
            }

            return semester;
        }

        // Changes the semester dates and windows. Nothing changes if any rule fails. A closed semester is locked.
        public Result Reschedule(DateOnly startDate, DateOnly endDate, SemesterWindows? windows, DateOnly yearStart, DateOnly yearEnd, DateTimeOffset now)
        {
            if (Status == SemesterStatus.Closed)
            {
                return Result.Fail(CalendarErrors.SemesterClosed);
            }

            var newWindows = windows ?? SemesterWindows.None;

            // Once registration has opened the registration and add/drop windows may not be removed.
            if (Status != SemesterStatus.Planned && !newWindows.HasRegistrationAndAddDrop)
            {
                return Result.Fail(CalendarErrors.SemesterWindowsIncomplete);
            }

            var applied = ApplySchedule(startDate, endDate, newWindows, yearStart, yearEnd);
            if (applied.IsFailure)
            {
                return applied;
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        // Moves the status forward. Leaving Planned needs the registration and add/drop windows.
        // Closed can only be reached from GradingOpen.
        public Result AdvanceTo(SemesterStatus target, DateTimeOffset now)
        {
            if (!Enum.IsDefined(target))
            {
                return Result.Fail(CalendarErrors.InvalidSemesterStatus);
            }

            if (Status == SemesterStatus.Closed)
            {
                return Result.Fail(CalendarErrors.SemesterClosed);
            }

            if (target <= Status)
            {
                return Result.Fail(CalendarErrors.SemesterInvalidState);
            }

            if (Status == SemesterStatus.Planned && !Windows.HasRegistrationAndAddDrop)
            {
                return Result.Fail(CalendarErrors.SemesterWindowsIncomplete);
            }

            if (target == SemesterStatus.Closed && Status != SemesterStatus.GradingOpen)
            {
                return Result.Fail(CalendarErrors.SemesterInvalidState);
            }

            Status = target;
            UpdatedAt = now;
            return Result.Ok();
        }

        public bool IsClosed => Status == SemesterStatus.Closed;

        public bool ContainsDate(DateOnly today) => today >= StartDate && today <= EndDate;

        public bool IsRegistrationOpen(DateOnly today)
            => !IsClosed && RegistrationStart is { } s && RegistrationEnd is { } e && today >= s && today <= e;

        public bool IsAddDropOpen(DateOnly today)
            => !IsClosed && AddDropStart is { } s && AddDropEnd is { } e && today >= s && today <= e;

        // Withdrawal is possible after add/drop has ended, up to and including the withdrawal deadline.
        public bool IsWithdrawalOpen(DateOnly today)
            => !IsClosed && AddDropEnd is { } ade && WithdrawalDeadline is { } wd && today > ade && today <= wd;

        // Grades can be entered from the first day of the semester until the submission deadline.
        public bool IsGradeSubmissionOpen(DateOnly today)
            => !IsClosed && GradeSubmissionDeadline is { } deadline && today >= StartDate && today <= deadline;

        public bool IsGradePublicationReached(DateOnly today)
            => GradePublicationDate is { } date && today >= date;

        private Result ApplySchedule(DateOnly startDate, DateOnly endDate, SemesterWindows windows, DateOnly yearStart, DateOnly yearEnd)
        {
            if (startDate >= endDate)
            {
                return Result.Fail(CalendarErrors.SemesterInvalidDates);
            }

            if (startDate < yearStart || endDate > yearEnd)
            {
                return Result.Fail(CalendarErrors.SemesterOutsideYear);
            }

            var valid = windows.Validate(startDate, endDate);
            if (valid.IsFailure)
            {
                return valid;
            }

            StartDate = startDate;
            EndDate = endDate;
            RegistrationStart = windows.RegistrationStart;
            RegistrationEnd = windows.RegistrationEnd;
            AddDropStart = windows.AddDropStart;
            AddDropEnd = windows.AddDropEnd;
            WithdrawalDeadline = windows.WithdrawalDeadline;
            MidtermStart = windows.MidtermStart;
            MidtermEnd = windows.MidtermEnd;
            FinalExamStart = windows.FinalExamStart;
            FinalExamEnd = windows.FinalExamEnd;
            GradeSubmissionDeadline = windows.GradeSubmissionDeadline;
            GradePublicationDate = windows.GradePublicationDate;
            return Result.Ok();
        }
    }
}
