using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.University
{
    public class FacultyTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Later = Now.AddDays(1);
        private static readonly Guid UniversityId = Guid.NewGuid();

        private static Faculty NewFaculty() => ResultAssert.Succeeded(
            Faculty.Create(UniversityId, "eng", "Faculty of Engineering", "Description", "eng@cu.edu", "+20 2 3567 6000", Now));

        [Fact]
        public void Create_with_valid_data_normalizes_code_and_starts_active()
        {
            var faculty = ResultAssert.Succeeded(Faculty.Create(UniversityId, " eng ", " Faculty of Engineering ", null, null, null, Now));

            Assert.NotEqual(Guid.Empty, faculty.Id);
            Assert.Equal(UniversityId, faculty.UniversityId);
            Assert.Equal("ENG", faculty.Code);
            Assert.Equal("Faculty of Engineering", faculty.Name);
            Assert.True(faculty.IsActive);
            Assert.Equal(Now, faculty.CreatedAt);
            Assert.Null(faculty.UpdatedAt);
        }

        [Fact]
        public void Create_rejects_empty_university_id()
        {
            ResultAssert.Failed(Faculty.Create(Guid.Empty, "ENG", "Faculty of Engineering", null, null, null, Now), UniversityErrors.InvalidParent);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("E")]
        [InlineData("EN G")]
        public void Create_rejects_invalid_code(string? code)
        {
            ResultAssert.Failed(Faculty.Create(UniversityId, code, "Faculty of Engineering", null, null, null, Now), UniversityErrors.InvalidCode);
        }

        [Fact]
        public void Create_rejects_invalid_name_email_and_phone()
        {
            ResultAssert.Failed(Faculty.Create(UniversityId, "ENG", " ", null, null, null, Now), UniversityErrors.InvalidName);
            ResultAssert.Failed(Faculty.Create(UniversityId, "ENG", "Faculty of Engineering", null, "bad", null, Now), UniversityErrors.InvalidEmail);
            ResultAssert.Failed(Faculty.Create(UniversityId, "ENG", "Faculty of Engineering", null, null, "12", Now), UniversityErrors.InvalidPhone);
        }

        [Fact]
        public void UpdateDetails_changes_fields_and_sets_UpdatedAt()
        {
            var faculty = NewFaculty();

            ResultAssert.Succeeded(faculty.UpdateDetails("Engineering", null, null, null, Later));

            Assert.Equal("Engineering", faculty.Name);
            Assert.Null(faculty.Email);
            Assert.Equal("ENG", faculty.Code);
            Assert.Equal(Later, faculty.UpdatedAt);
        }

        [Fact]
        public void UpdateDetails_with_invalid_data_changes_nothing()
        {
            var faculty = NewFaculty();

            ResultAssert.Failed(faculty.UpdateDetails("Engineering", "New", "bad", null, Later), UniversityErrors.InvalidEmail);

            Assert.Equal("Faculty of Engineering", faculty.Name);
            Assert.Null(faculty.UpdatedAt);
        }

        [Fact]
        public void Deactivate_with_active_departments_is_rejected()
        {
            var faculty = NewFaculty();

            ResultAssert.Failed(faculty.Deactivate(hasActiveDepartments: true, Later), UniversityErrors.FacultyHasActiveDepartments);
            Assert.True(faculty.IsActive);
        }

        [Fact]
        public void Deactivate_and_Activate_follow_the_state_rules()
        {
            var faculty = NewFaculty();

            ResultAssert.Failed(faculty.Activate(Later), UniversityErrors.AlreadyActive);
            ResultAssert.Succeeded(faculty.Deactivate(false, Later));
            Assert.False(faculty.IsActive);
            Assert.Equal(Later, faculty.UpdatedAt);

            ResultAssert.Failed(faculty.Deactivate(false, Later), UniversityErrors.AlreadyInactive);
            ResultAssert.Succeeded(faculty.Activate(Later));
            Assert.True(faculty.IsActive);
        }
    }
}
