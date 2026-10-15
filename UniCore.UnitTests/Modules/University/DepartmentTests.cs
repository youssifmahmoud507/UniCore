using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.University
{
    public class DepartmentTests
    {
        private static readonly Guid FacultyId = Guid.NewGuid();

        private static Department NewDepartment() => ResultAssert.Succeeded(
            Department.Create(FacultyId, "cse", "Computer Engineering", "Description", "cse@cu.edu", "+20 2 3567 6001"));

        [Fact]
        public void Create_with_valid_data_normalizes_code_and_starts_active()
        {
            var department = ResultAssert.Succeeded(Department.Create(FacultyId, " cse ", " Computer Engineering ", null, null, null));

            Assert.NotEqual(Guid.Empty, department.Id);
            Assert.Equal(FacultyId, department.FacultyId);
            Assert.Equal("CSE", department.Code);
            Assert.Equal("Computer Engineering", department.Name);
            Assert.Null(department.Description);
            Assert.True(department.IsActive);
        }

        [Fact]
        public void Create_rejects_empty_faculty_id()
        {
            ResultAssert.Failed(Department.Create(Guid.Empty, "CSE", "Computer Engineering", null, null, null), UniversityErrors.InvalidParent);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("C")]
        [InlineData("CS E")]
        public void Create_rejects_invalid_code(string? code)
        {
            ResultAssert.Failed(Department.Create(FacultyId, code, "Computer Engineering", null, null, null), UniversityErrors.InvalidCode);
        }

        [Fact]
        public void Create_rejects_invalid_name_description_email_and_phone()
        {
            ResultAssert.Failed(Department.Create(FacultyId, "CSE", "x", null, null, null), UniversityErrors.InvalidName);
            ResultAssert.Failed(Department.Create(FacultyId, "CSE", "Computer Engineering", new string('a', 1001), null, null), UniversityErrors.InvalidDescription);
            ResultAssert.Failed(Department.Create(FacultyId, "CSE", "Computer Engineering", null, "bad", null), UniversityErrors.InvalidEmail);
            ResultAssert.Failed(Department.Create(FacultyId, "CSE", "Computer Engineering", null, null, "abc"), UniversityErrors.InvalidPhone);
        }

        [Fact]
        public void UpdateDetails_changes_fields_but_never_the_code()
        {
            var department = NewDepartment();

            ResultAssert.Succeeded(department.UpdateDetails("Computer and Systems", null, null, null));

            Assert.Equal("Computer and Systems", department.Name);
            Assert.Equal("CSE", department.Code);
        }

        [Fact]
        public void UpdateDetails_with_invalid_data_changes_nothing()
        {
            var department = NewDepartment();

            ResultAssert.Failed(department.UpdateDetails("Other", "New", null, "abc"), UniversityErrors.InvalidPhone);

            Assert.Equal("Computer Engineering", department.Name);
            Assert.Equal("Description", department.Description);
        }

        [Fact]
        public void Deactivate_with_active_programs_is_rejected()
        {
            var department = NewDepartment();

            ResultAssert.Failed(department.Deactivate(hasActivePrograms: true), UniversityErrors.DepartmentHasActivePrograms);
            Assert.True(department.IsActive);
        }

        [Fact]
        public void Deactivate_and_Activate_follow_the_state_rules()
        {
            var department = NewDepartment();

            ResultAssert.Failed(department.Activate(), UniversityErrors.AlreadyActive);
            ResultAssert.Succeeded(department.Deactivate(false));
            Assert.False(department.IsActive);

            ResultAssert.Failed(department.Deactivate(false), UniversityErrors.AlreadyInactive);
            ResultAssert.Succeeded(department.Activate());
            Assert.True(department.IsActive);
        }
    }
}
