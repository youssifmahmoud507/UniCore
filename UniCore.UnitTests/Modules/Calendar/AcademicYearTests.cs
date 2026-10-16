using UniCore.Domain.Modules.Calendar;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Calendar
{
    public class AcademicYearTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Start = new(2026, 9, 1);
        private static readonly DateOnly End = new(2027, 8, 31);

        private static AcademicYear NewYear() => ResultAssert.Succeeded(AcademicYear.Create("2026/2027", Start, End, Now));

        [Fact]
        public void Create_with_valid_data_starts_planned_and_not_current()
        {
            var year = ResultAssert.Succeeded(AcademicYear.Create(" 2026/2027 ", Start, End, Now));

            Assert.NotEqual(Guid.Empty, year.Id);
            Assert.Equal("2026/2027", year.Name);
            Assert.Equal(AcademicYearStatus.Planned, year.Status);
            Assert.False(year.IsCurrent);
            Assert.Equal(Now, year.CreatedAt);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("A")]
        public void Create_rejects_invalid_name(string? name)
        {
            ResultAssert.Failed(AcademicYear.Create(name, Start, End, Now), CalendarErrors.InvalidName);
        }

        [Fact]
        public void Create_rejects_start_that_is_not_before_end()
        {
            ResultAssert.Failed(AcademicYear.Create("Y", Start, Start, Now), CalendarErrors.InvalidName); // name too short wins
            ResultAssert.Failed(AcademicYear.Create("2026/2027", Start, Start, Now), CalendarErrors.AcademicYearInvalidDates);
            ResultAssert.Failed(AcademicYear.Create("2026/2027", End, Start, Now), CalendarErrors.AcademicYearInvalidDates);
        }

        [Fact]
        public void Activate_works_once_from_planned()
        {
            var year = NewYear();

            ResultAssert.Succeeded(year.Activate(Now));
            Assert.Equal(AcademicYearStatus.Active, year.Status);
            ResultAssert.Failed(year.Activate(Now), CalendarErrors.AcademicYearInvalidState);
        }

        [Fact]
        public void MakeCurrent_needs_an_active_year()
        {
            var year = NewYear();

            ResultAssert.Failed(year.MakeCurrent(false, Now), CalendarErrors.AcademicYearInvalidState);
            Assert.False(year.IsCurrent);
        }

        [Fact]
        public void MakeCurrent_is_rejected_when_another_year_is_current()
        {
            var year = NewYear();
            ResultAssert.Succeeded(year.Activate(Now));

            ResultAssert.Failed(year.MakeCurrent(anotherYearIsCurrent: true, Now), CalendarErrors.AcademicYearCurrentExists);
            Assert.False(year.IsCurrent);

            ResultAssert.Succeeded(year.MakeCurrent(false, Now));
            Assert.True(year.IsCurrent);
            ResultAssert.Failed(year.MakeCurrent(false, Now), CalendarErrors.AcademicYearInvalidState);
        }

        [Fact]
        public void Close_needs_an_active_year_without_open_semesters_and_clears_current()
        {
            var year = NewYear();
            ResultAssert.Failed(year.Close(false, Now), CalendarErrors.AcademicYearInvalidState);

            ResultAssert.Succeeded(year.Activate(Now));
            ResultAssert.Succeeded(year.MakeCurrent(false, Now));
            ResultAssert.Failed(year.Close(hasOpenSemesters: true, Now), CalendarErrors.AcademicYearHasOpenSemesters);
            Assert.True(year.IsCurrent);

            ResultAssert.Succeeded(year.Close(false, Now));
            Assert.Equal(AcademicYearStatus.Closed, year.Status);
            Assert.False(year.IsCurrent);
        }

        [Fact]
        public void A_closed_year_is_read_only()
        {
            var year = NewYear();
            ResultAssert.Succeeded(year.Activate(Now));
            ResultAssert.Succeeded(year.Close(false, Now));

            ResultAssert.Failed(year.UpdateDetails("New", Start, End, Now), CalendarErrors.AcademicYearClosed);
            Assert.Equal("2026/2027", year.Name);
        }

        [Fact]
        public void UpdateDetails_with_invalid_data_changes_nothing()
        {
            var year = NewYear();

            ResultAssert.Failed(year.UpdateDetails("2027/2028", End, Start, Now), CalendarErrors.AcademicYearInvalidDates);

            Assert.Equal("2026/2027", year.Name);
            Assert.Equal(Start, year.StartDate);
            Assert.Null(year.UpdatedAt);
        }

        [Fact]
        public void Contains_and_Overlaps_use_inclusive_dates()
        {
            var year = NewYear();

            Assert.True(year.Contains(Start, End));
            Assert.False(year.Contains(Start.AddDays(-1), End));
            Assert.True(year.Overlaps(End, End.AddDays(30)));
            Assert.False(year.Overlaps(End.AddDays(1), End.AddDays(30)));
        }
    }
}
