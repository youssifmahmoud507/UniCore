using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.University
{
    public class AcademicProgramTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Later = Now.AddDays(1);
        private static readonly Guid DepartmentId = Guid.NewGuid();

        private static AcademicProgram NewProgram() => ResultAssert.Succeeded(
            AcademicProgram.Create(DepartmentId, "bsc-cse", "BSc Computer Engineering", DegreeType.Bachelor, 5, 180, Now));

        [Fact]
        public void Create_with_valid_data_normalizes_code_and_starts_active()
        {
            var program = ResultAssert.Succeeded(
                AcademicProgram.Create(DepartmentId, " bsc-cse ", " BSc Computer Engineering ", DegreeType.Bachelor, 5, 180, Now));

            Assert.NotEqual(Guid.Empty, program.Id);
            Assert.Equal(DepartmentId, program.DepartmentId);
            Assert.Equal("BSC-CSE", program.Code);
            Assert.Equal("BSc Computer Engineering", program.Name);
            Assert.Equal(DegreeType.Bachelor, program.DegreeType);
            Assert.Equal(5, program.DurationYears);
            Assert.Equal(180, program.TotalRequiredCredits);
            Assert.True(program.IsActive);
            Assert.Equal(Now, program.CreatedAt);
            Assert.Null(program.UpdatedAt);
        }

        [Fact]
        public void Create_rejects_empty_department_id()
        {
            ResultAssert.Failed(AcademicProgram.Create(Guid.Empty, "BSC", "Program", DegreeType.Bachelor, 4, 150, Now), UniversityErrors.InvalidParent);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("B")]
        [InlineData("BSC CSE")]
        public void Create_rejects_invalid_code(string? code)
        {
            ResultAssert.Failed(AcademicProgram.Create(DepartmentId, code, "Program", DegreeType.Bachelor, 4, 150, Now), UniversityErrors.InvalidCode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" ")]
        [InlineData("P")]
        public void Create_rejects_invalid_name(string? name)
        {
            ResultAssert.Failed(AcademicProgram.Create(DepartmentId, "BSC", name, DegreeType.Bachelor, 4, 150, Now), UniversityErrors.InvalidName);
        }

        [Fact]
        public void Create_rejects_an_undefined_degree_type()
        {
            ResultAssert.Failed(AcademicProgram.Create(DepartmentId, "BSC", "Program", (DegreeType)99, 4, 150, Now), UniversityErrors.InvalidDegreeType);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(11)]
        public void Create_rejects_duration_outside_1_to_10_years(int years)
        {
            ResultAssert.Failed(AcademicProgram.Create(DepartmentId, "BSC", "Program", DegreeType.Bachelor, years, 150, Now), UniversityErrors.InvalidDuration);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        [InlineData(401)]
        public void Create_rejects_credits_outside_1_to_400(int credits)
        {
            ResultAssert.Failed(AcademicProgram.Create(DepartmentId, "BSC", "Program", DegreeType.Bachelor, 4, credits, Now), UniversityErrors.InvalidRequiredCredits);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(10, 400)]
        public void Create_accepts_the_boundaries(int years, int credits)
        {
            var program = ResultAssert.Succeeded(AcademicProgram.Create(DepartmentId, "BSC", "Program", DegreeType.Doctorate, years, credits, Now));

            Assert.Equal(years, program.DurationYears);
            Assert.Equal(credits, program.TotalRequiredCredits);
        }

        [Fact]
        public void UpdateDetails_changes_fields_sets_UpdatedAt_and_keeps_the_code()
        {
            var program = NewProgram();

            ResultAssert.Succeeded(program.UpdateDetails("MSc Computer Engineering", DegreeType.Master, 2, 60, Later));

            Assert.Equal("MSc Computer Engineering", program.Name);
            Assert.Equal(DegreeType.Master, program.DegreeType);
            Assert.Equal(2, program.DurationYears);
            Assert.Equal(60, program.TotalRequiredCredits);
            Assert.Equal("BSC-CSE", program.Code);
            Assert.Equal(Later, program.UpdatedAt);
        }

        [Fact]
        public void UpdateDetails_with_invalid_data_changes_nothing()
        {
            var program = NewProgram();

            ResultAssert.Failed(program.UpdateDetails("New name", DegreeType.Master, 0, 60, Later), UniversityErrors.InvalidDuration);

            Assert.Equal("BSc Computer Engineering", program.Name);
            Assert.Equal(DegreeType.Bachelor, program.DegreeType);
            Assert.Equal(5, program.DurationYears);
            Assert.Null(program.UpdatedAt);
        }

        [Fact]
        public void Deactivate_with_active_students_is_rejected()
        {
            var program = NewProgram();

            ResultAssert.Failed(program.Deactivate(hasActiveStudents: true, Later), UniversityErrors.ProgramHasActiveStudents);
            Assert.True(program.IsActive);
        }

        [Fact]
        public void Deactivate_and_Activate_follow_the_state_rules()
        {
            var program = NewProgram();

            ResultAssert.Failed(program.Activate(Later), UniversityErrors.AlreadyActive);

            ResultAssert.Succeeded(program.Deactivate(false, Later));
            Assert.False(program.IsActive);
            Assert.Equal(Later, program.UpdatedAt);

            ResultAssert.Failed(program.Deactivate(false, Later), UniversityErrors.AlreadyInactive);

            ResultAssert.Succeeded(program.Activate(Later.AddDays(1)));
            Assert.True(program.IsActive);
        }
    }
}
