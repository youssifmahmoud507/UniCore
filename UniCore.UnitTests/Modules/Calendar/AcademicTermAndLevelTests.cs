using UniCore.Domain.Modules.Calendar;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Calendar
{
    public class AcademicTermTests
    {
        [Fact]
        public void Create_normalizes_the_code_and_starts_active()
        {
            var term = ResultAssert.Succeeded(AcademicTerm.Create(" fall ", " Fall ", 1));

            Assert.Equal("FALL", term.Code);
            Assert.Equal("Fall", term.Name);
            Assert.Equal(1, term.SortOrder);
            Assert.True(term.IsActive);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("F")]
        [InlineData("FA LL")]
        public void Create_rejects_invalid_code(string? code)
        {
            ResultAssert.Failed(AcademicTerm.Create(code, "Fall", 1), CalendarErrors.InvalidCode);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(11)]
        public void Create_rejects_sort_order_outside_1_to_10(int order)
        {
            ResultAssert.Failed(AcademicTerm.Create("FALL", "Fall", order), CalendarErrors.InvalidTermOrder);
        }

        [Fact]
        public void Create_rejects_an_invalid_name()
        {
            ResultAssert.Failed(AcademicTerm.Create("FALL", " ", 1), CalendarErrors.InvalidName);
        }

        [Fact]
        public void Update_never_changes_the_code_and_Deactivate_follows_the_state_rules()
        {
            var term = ResultAssert.Succeeded(AcademicTerm.Create("FALL", "Fall", 1));

            ResultAssert.Succeeded(term.Update("Autumn", 2));
            Assert.Equal("FALL", term.Code);
            Assert.Equal("Autumn", term.Name);

            ResultAssert.Failed(term.Activate(), CalendarErrors.AlreadyActive);
            ResultAssert.Succeeded(term.Deactivate());
            ResultAssert.Failed(term.Deactivate(), CalendarErrors.AlreadyInactive);
            ResultAssert.Succeeded(term.Activate());
        }
    }

    public class AcademicLevelTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        private static readonly Guid ProgramId = Guid.NewGuid();

        private static AcademicLevel NewLevel() => ResultAssert.Succeeded(AcademicLevel.Create(ProgramId, "Level 2", 2, 36, Now));

        [Fact]
        public void Create_with_valid_data_starts_active()
        {
            var level = NewLevel();

            Assert.Equal(ProgramId, level.AcademicProgramId);
            Assert.Equal("Level 2", level.Name);
            Assert.Equal(2, level.Order);
            Assert.Equal(36, level.RequiredCredits);
            Assert.True(level.IsActive);
        }

        [Fact]
        public void The_first_level_may_need_zero_credits()
        {
            var level = ResultAssert.Succeeded(AcademicLevel.Create(ProgramId, "Level 1", 1, 0, Now));

            Assert.Equal(0, level.RequiredCredits);
        }

        [Fact]
        public void Create_rejects_empty_program_id_and_invalid_name()
        {
            ResultAssert.Failed(AcademicLevel.Create(Guid.Empty, "Level 1", 1, 0, Now), CalendarErrors.InvalidParent);
            ResultAssert.Failed(AcademicLevel.Create(ProgramId, "", 1, 0, Now), CalendarErrors.InvalidName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(21)]
        public void Create_rejects_order_outside_1_to_20(int order)
        {
            ResultAssert.Failed(AcademicLevel.Create(ProgramId, "Level", order, 0, Now), CalendarErrors.InvalidLevelOrder);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(401)]
        public void Create_rejects_credits_outside_0_to_400(int credits)
        {
            ResultAssert.Failed(AcademicLevel.Create(ProgramId, "Level", 1, credits, Now), CalendarErrors.InvalidLevelCredits);
        }

        [Fact]
        public void Update_with_invalid_data_changes_nothing()
        {
            var level = NewLevel();

            ResultAssert.Failed(level.Update("Level 3", 3, 500, Now), CalendarErrors.InvalidLevelCredits);

            Assert.Equal("Level 2", level.Name);
            Assert.Equal(2, level.Order);
            Assert.Null(level.UpdatedAt);
        }

        [Fact]
        public void Deactivate_and_Activate_follow_the_state_rules()
        {
            var level = NewLevel();

            ResultAssert.Failed(level.Activate(Now), CalendarErrors.AlreadyActive);
            ResultAssert.Succeeded(level.Deactivate(Now));
            ResultAssert.Failed(level.Deactivate(Now), CalendarErrors.AlreadyInactive);
            ResultAssert.Succeeded(level.Activate(Now));
        }
    }
}
