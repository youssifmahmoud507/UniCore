using UniCore.Domain.Modules.Calendar;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Calendar
{
    public class AcademicSemesterTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        private static readonly Guid YearId = Guid.NewGuid();
        private static readonly Guid TermId = Guid.NewGuid();

        private static readonly DateOnly YearStart = new(2026, 9, 1);
        private static readonly DateOnly YearEnd = new(2027, 8, 31);

        // Fall 2026: 1 Sep - 31 Jan.
        private static readonly DateOnly Start = new(2026, 9, 1);
        private static readonly DateOnly End = new(2027, 1, 31);

        private static SemesterWindows FullWindows() => new(
            RegistrationStart: new(2026, 9, 1), RegistrationEnd: new(2026, 9, 10),
            AddDropStart: new(2026, 9, 5), AddDropEnd: new(2026, 9, 20),
            WithdrawalDeadline: new(2026, 11, 15),
            MidtermStart: new(2026, 11, 1), MidtermEnd: new(2026, 11, 10),
            FinalExamStart: new(2027, 1, 5), FinalExamEnd: new(2027, 1, 20),
            GradeSubmissionDeadline: new(2027, 1, 28), GradePublicationDate: new(2027, 2, 3));

        private static SemesterWindows RegistrationOnly() => SemesterWindows.None with
        {
            RegistrationStart = new(2026, 9, 1), RegistrationEnd = new(2026, 9, 10),
            AddDropStart = new(2026, 9, 5), AddDropEnd = new(2026, 9, 20)
        };

        private static AcademicSemester NewSemester(SemesterWindows? windows = null)
            => ResultAssert.Succeeded(AcademicSemester.Create(YearId, TermId, Start, End, windows ?? FullWindows(), YearStart, YearEnd, Now));

        // ---------- create ----------

        [Fact]
        public void Create_with_valid_data_starts_planned_and_keeps_the_windows()
        {
            var semester = NewSemester();

            Assert.Equal(SemesterStatus.Planned, semester.Status);
            Assert.Equal(YearId, semester.AcademicYearId);
            Assert.Equal(TermId, semester.AcademicTermId);
            Assert.Equal(FullWindows(), semester.Windows);
        }

        [Fact]
        public void Create_without_windows_is_allowed_while_planned()
        {
            var semester = ResultAssert.Succeeded(AcademicSemester.Create(YearId, TermId, Start, End, null, YearStart, YearEnd, Now));

            Assert.Equal(SemesterWindows.None, semester.Windows);
        }

        [Fact]
        public void Create_rejects_empty_parent_ids()
        {
            ResultAssert.Failed(AcademicSemester.Create(Guid.Empty, TermId, Start, End, null, YearStart, YearEnd, Now), CalendarErrors.InvalidParent);
            ResultAssert.Failed(AcademicSemester.Create(YearId, Guid.Empty, Start, End, null, YearStart, YearEnd, Now), CalendarErrors.InvalidParent);
        }

        [Fact]
        public void Create_rejects_start_not_before_end()
        {
            ResultAssert.Failed(AcademicSemester.Create(YearId, TermId, End, Start, null, YearStart, YearEnd, Now), CalendarErrors.SemesterInvalidDates);
            ResultAssert.Failed(AcademicSemester.Create(YearId, TermId, Start, Start, null, YearStart, YearEnd, Now), CalendarErrors.SemesterInvalidDates);
        }

        [Fact]
        public void Create_rejects_dates_outside_the_year()
        {
            ResultAssert.Failed(AcademicSemester.Create(YearId, TermId, YearStart.AddDays(-1), End, null, YearStart, YearEnd, Now), CalendarErrors.SemesterOutsideYear);
            ResultAssert.Failed(AcademicSemester.Create(YearId, TermId, Start, YearEnd.AddDays(1), null, YearStart, YearEnd, Now), CalendarErrors.SemesterOutsideYear);
        }

        // ---------- window rules ----------

        public static TheoryData<string, SemesterWindows> InvalidWindows() => new()
        {
            { "registration start without end", SemesterWindows.None with { RegistrationStart = new DateOnly(2026, 9, 1) } },
            { "registration end not after start", SemesterWindows.None with { RegistrationStart = new DateOnly(2026, 9, 10), RegistrationEnd = new DateOnly(2026, 9, 10) } },
            { "registration before the semester", SemesterWindows.None with { RegistrationStart = new DateOnly(2026, 8, 20), RegistrationEnd = new DateOnly(2026, 9, 10) } },
            { "registration after the semester", SemesterWindows.None with { RegistrationStart = new DateOnly(2027, 1, 1), RegistrationEnd = new DateOnly(2027, 2, 10) } },
            { "add/drop starts before registration", FullWindows() with { AddDropStart = new DateOnly(2026, 8, 31), AddDropEnd = new DateOnly(2026, 9, 20) } },
            { "add/drop ends after the semester", FullWindows() with { AddDropEnd = new DateOnly(2027, 2, 1) } },
            { "withdrawal not after add/drop end", FullWindows() with { WithdrawalDeadline = new DateOnly(2026, 9, 20) } },
            { "withdrawal after the semester", FullWindows() with { WithdrawalDeadline = new DateOnly(2027, 2, 1) } },
            { "midterm end before start", FullWindows() with { MidtermStart = new DateOnly(2026, 11, 10), MidtermEnd = new DateOnly(2026, 11, 1) } },
            { "midterm outside the semester", FullWindows() with { MidtermStart = new DateOnly(2027, 1, 30), MidtermEnd = new DateOnly(2027, 2, 5) } },
            { "final exams start before midterm ends", FullWindows() with { FinalExamStart = new DateOnly(2026, 11, 10) } },
            { "final exam end before start", FullWindows() with { FinalExamStart = new DateOnly(2027, 1, 20), FinalExamEnd = new DateOnly(2027, 1, 10) } },
            { "final exams after the semester", FullWindows() with { FinalExamStart = new DateOnly(2027, 1, 25), FinalExamEnd = new DateOnly(2027, 2, 5) } },
            { "grade deadline before the final exams end", FullWindows() with { GradeSubmissionDeadline = new DateOnly(2027, 1, 19) } },
            { "publication before the grade deadline", FullWindows() with { GradePublicationDate = new DateOnly(2027, 1, 27) } },
        };

        [Theory]
        [MemberData(nameof(InvalidWindows))]
        public void Create_rejects_windows_that_break_the_order(string reason, SemesterWindows windows)
        {
            var result = AcademicSemester.Create(YearId, TermId, Start, End, windows, YearStart, YearEnd, Now);

            Assert.True(result.IsFailure, reason);
            Assert.Equal(CalendarErrors.SemesterInvalidDates.Code, result.Error?.Code);
        }

        [Fact]
        public void Grade_dates_may_fall_after_the_semester_end()
        {
            var windows = FullWindows() with { GradeSubmissionDeadline = new DateOnly(2027, 2, 10), GradePublicationDate = new DateOnly(2027, 2, 15) };

            ResultAssert.Succeeded(AcademicSemester.Create(YearId, TermId, Start, End, windows, YearStart, YearEnd, Now));
        }

        // ---------- status ----------

        [Fact]
        public void AdvanceTo_moves_forward_only()
        {
            var semester = NewSemester();

            ResultAssert.Succeeded(semester.AdvanceTo(SemesterStatus.RegistrationOpen, Now));
            ResultAssert.Succeeded(semester.AdvanceTo(SemesterStatus.InProgress, Now));
            ResultAssert.Failed(semester.AdvanceTo(SemesterStatus.RegistrationOpen, Now), CalendarErrors.SemesterInvalidState);
            ResultAssert.Failed(semester.AdvanceTo(SemesterStatus.InProgress, Now), CalendarErrors.SemesterInvalidState);
            Assert.Equal(SemesterStatus.InProgress, semester.Status);
        }

        [Fact]
        public void AdvanceTo_from_planned_needs_the_registration_and_add_drop_windows()
        {
            var semester = NewSemester(SemesterWindows.None);

            ResultAssert.Failed(semester.AdvanceTo(SemesterStatus.RegistrationOpen, Now), CalendarErrors.SemesterWindowsIncomplete);
            Assert.Equal(SemesterStatus.Planned, semester.Status);

            ResultAssert.Succeeded(semester.Reschedule(Start, End, RegistrationOnly(), YearStart, YearEnd, Now));
            ResultAssert.Succeeded(semester.AdvanceTo(SemesterStatus.RegistrationOpen, Now));
        }

        [Fact]
        public void AdvanceTo_rejects_an_undefined_status()
        {
            ResultAssert.Failed(NewSemester().AdvanceTo((SemesterStatus)99, Now), CalendarErrors.InvalidSemesterStatus);
        }

        [Fact]
        public void Closed_can_only_be_reached_from_grading_open_and_locks_the_semester()
        {
            var semester = NewSemester();
            ResultAssert.Succeeded(semester.AdvanceTo(SemesterStatus.Examinations, Now));

            ResultAssert.Failed(semester.AdvanceTo(SemesterStatus.Closed, Now), CalendarErrors.SemesterInvalidState);

            ResultAssert.Succeeded(semester.AdvanceTo(SemesterStatus.GradingOpen, Now));
            ResultAssert.Succeeded(semester.AdvanceTo(SemesterStatus.Closed, Now));

            Assert.True(semester.IsClosed);
            ResultAssert.Failed(semester.AdvanceTo(SemesterStatus.Closed, Now), CalendarErrors.SemesterClosed);
            ResultAssert.Failed(semester.Reschedule(Start, End, FullWindows(), YearStart, YearEnd, Now), CalendarErrors.SemesterClosed);
        }

        // ---------- reschedule ----------

        [Fact]
        public void Reschedule_with_an_invalid_window_changes_nothing()
        {
            var semester = NewSemester();

            ResultAssert.Failed(
                semester.Reschedule(Start, End, FullWindows() with { WithdrawalDeadline = new DateOnly(2026, 9, 1) }, YearStart, YearEnd, Now),
                CalendarErrors.SemesterInvalidDates);

            Assert.Equal(FullWindows(), semester.Windows);
            Assert.Null(semester.UpdatedAt);
        }

        [Fact]
        public void Reschedule_cannot_remove_registration_windows_once_registration_has_opened()
        {
            var semester = NewSemester();
            ResultAssert.Succeeded(semester.AdvanceTo(SemesterStatus.RegistrationOpen, Now));

            ResultAssert.Failed(semester.Reschedule(Start, End, SemesterWindows.None, YearStart, YearEnd, Now), CalendarErrors.SemesterWindowsIncomplete);
            ResultAssert.Succeeded(semester.Reschedule(Start, End, RegistrationOnly(), YearStart, YearEnd, Now));
            Assert.Null(semester.WithdrawalDeadline);
        }

        // ---------- "is the window open today?" ----------

        [Theory]
        [InlineData(2026, 8, 31, false)]
        [InlineData(2026, 9, 1, true)]    // first day counts
        [InlineData(2026, 9, 10, true)]   // last day counts
        [InlineData(2026, 9, 11, false)]
        public void IsRegistrationOpen_includes_both_ends(int year, int month, int day, bool expected)
        {
            Assert.Equal(expected, NewSemester().IsRegistrationOpen(new DateOnly(year, month, day)));
        }

        [Theory]
        [InlineData(2026, 9, 4, false)]
        [InlineData(2026, 9, 5, true)]
        [InlineData(2026, 9, 20, true)]
        [InlineData(2026, 9, 21, false)]
        public void IsAddDropOpen_includes_both_ends(int year, int month, int day, bool expected)
        {
            Assert.Equal(expected, NewSemester().IsAddDropOpen(new DateOnly(year, month, day)));
        }

        [Theory]
        [InlineData(2026, 9, 20, false)]  // still add/drop
        [InlineData(2026, 9, 21, true)]   // the day after add/drop ends
        [InlineData(2026, 11, 15, true)]  // deadline day counts
        [InlineData(2026, 11, 16, false)]
        public void IsWithdrawalOpen_runs_from_after_add_drop_to_the_deadline(int year, int month, int day, bool expected)
        {
            Assert.Equal(expected, NewSemester().IsWithdrawalOpen(new DateOnly(year, month, day)));
        }

        [Theory]
        [InlineData(2026, 8, 31, false)]
        [InlineData(2026, 12, 1, true)]
        [InlineData(2027, 1, 28, true)]
        [InlineData(2027, 1, 29, false)]
        public void IsGradeSubmissionOpen_runs_from_the_start_to_the_deadline(int year, int month, int day, bool expected)
        {
            Assert.Equal(expected, NewSemester().IsGradeSubmissionOpen(new DateOnly(year, month, day)));
        }

        [Fact]
        public void IsGradePublicationReached_is_true_from_the_publication_date()
        {
            var semester = NewSemester();

            Assert.False(semester.IsGradePublicationReached(new DateOnly(2027, 2, 2)));
            Assert.True(semester.IsGradePublicationReached(new DateOnly(2027, 2, 3)));
        }

        [Fact]
        public void Windows_that_are_not_set_are_never_open()
        {
            var semester = NewSemester(SemesterWindows.None);
            var today = new DateOnly(2026, 9, 5);

            Assert.False(semester.IsRegistrationOpen(today));
            Assert.False(semester.IsAddDropOpen(today));
            Assert.False(semester.IsWithdrawalOpen(today));
            Assert.False(semester.IsGradeSubmissionOpen(today));
            Assert.False(semester.IsGradePublicationReached(today));
        }

        [Fact]
        public void A_closed_semester_has_no_open_window()
        {
            var semester = NewSemester();
            ResultAssert.Succeeded(semester.AdvanceTo(SemesterStatus.GradingOpen, Now));
            ResultAssert.Succeeded(semester.AdvanceTo(SemesterStatus.Closed, Now));

            Assert.False(semester.IsRegistrationOpen(new DateOnly(2026, 9, 5)));
            Assert.False(semester.IsAddDropOpen(new DateOnly(2026, 9, 6)));
            Assert.False(semester.IsGradeSubmissionOpen(new DateOnly(2026, 12, 1)));
        }

        [Fact]
        public void ContainsDate_includes_both_ends()
        {
            var semester = NewSemester();

            Assert.True(semester.ContainsDate(Start));
            Assert.True(semester.ContainsDate(End));
            Assert.False(semester.ContainsDate(End.AddDays(1)));
        }
    }
}
