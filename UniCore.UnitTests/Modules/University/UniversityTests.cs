using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;

// The namespace UniCore.UnitTests.Modules.University hides the class name, so the entity gets an alias.
using UniversityEntity = UniCore.Domain.Modules.University.University;

namespace UniCore.UnitTests.Modules.University
{
    public class UniversityTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Later = Now.AddDays(1);

        private static UniversityEntity NewUniversity() => ResultAssert.Succeeded(
            UniversityEntity.Create("cu", "Cairo University", "Description", "info@cu.edu", "+20 2 3567 6105", "https://cu.edu", Now));

        [Fact]
        public void Create_with_valid_data_normalizes_code_and_starts_active()
        {
            var university = ResultAssert.Succeeded(
                UniversityEntity.Create("  cu  ", "  Cairo University ", " A description ", " info@cu.edu ", " +20 2 3567 6105 ", " https://cu.edu ", Now));

            Assert.NotEqual(Guid.Empty, university.Id);
            Assert.Equal("CU", university.Code);
            Assert.Equal("Cairo University", university.Name);
            Assert.Equal("A description", university.Description);
            Assert.Equal("info@cu.edu", university.Email);
            Assert.Equal("+20 2 3567 6105", university.Phone);
            Assert.Equal("https://cu.edu", university.Website);
            Assert.True(university.IsActive);
            Assert.Equal(Now, university.CreatedAt);
            Assert.Null(university.UpdatedAt);
        }

        [Fact]
        public void Create_turns_blank_optional_fields_into_null()
        {
            var university = ResultAssert.Succeeded(UniversityEntity.Create("CU", "Cairo University", " ", "", null, "  ", Now));

            Assert.Null(university.Description);
            Assert.Null(university.Email);
            Assert.Null(university.Phone);
            Assert.Null(university.Website);
        }

        [Fact]
        public void Create_gives_each_university_its_own_id()
        {
            Assert.NotEqual(NewUniversity().Id, NewUniversity().Id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("C")]
        [InlineData("CU CU")]
        [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
        public void Create_rejects_invalid_code(string? code)
        {
            ResultAssert.Failed(UniversityEntity.Create(code, "Cairo University", null, null, null, null, Now), UniversityErrors.InvalidCode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" ")]
        [InlineData("A")]
        public void Create_rejects_invalid_name(string? name)
        {
            ResultAssert.Failed(UniversityEntity.Create("CU", name, null, null, null, null, Now), UniversityErrors.InvalidName);
        }

        [Fact]
        public void Create_rejects_too_long_name_and_description()
        {
            ResultAssert.Failed(UniversityEntity.Create("CU", new string('a', 201), null, null, null, null, Now), UniversityErrors.InvalidName);
            ResultAssert.Failed(UniversityEntity.Create("CU", "Cairo University", new string('a', 1001), null, null, null, Now), UniversityErrors.InvalidDescription);
        }

        [Fact]
        public void Create_rejects_invalid_email_phone_and_website()
        {
            ResultAssert.Failed(UniversityEntity.Create("CU", "Cairo University", null, "not-an-email", null, null, Now), UniversityErrors.InvalidEmail);
            ResultAssert.Failed(UniversityEntity.Create("CU", "Cairo University", null, null, "abc", null, Now), UniversityErrors.InvalidPhone);
            ResultAssert.Failed(UniversityEntity.Create("CU", "Cairo University", null, null, null, "cu.edu", Now), UniversityErrors.InvalidWebsite);
        }

        [Fact]
        public void UpdateDetails_changes_fields_and_sets_UpdatedAt()
        {
            var university = NewUniversity();

            ResultAssert.Succeeded(university.UpdateDetails("Cairo Univ.", null, null, null, null, Later));

            Assert.Equal("Cairo Univ.", university.Name);
            Assert.Null(university.Description);
            Assert.Null(university.Email);
            Assert.Equal("CU", university.Code); // the code never changes
            Assert.Equal(Later, university.UpdatedAt);
        }

        [Fact]
        public void UpdateDetails_with_invalid_data_changes_nothing()
        {
            var university = NewUniversity();

            var result = university.UpdateDetails("New name", "New description", "bad-email", null, null, Later);

            ResultAssert.Failed(result, UniversityErrors.InvalidEmail);
            Assert.Equal("Cairo University", university.Name);
            Assert.Equal("Description", university.Description);
            Assert.Null(university.UpdatedAt);
        }

        [Fact]
        public void Deactivate_with_active_children_is_rejected()
        {
            var university = NewUniversity();

            ResultAssert.Failed(university.Deactivate(hasActiveChildren: true, Later), UniversityErrors.UniversityHasActiveChildren);
            Assert.True(university.IsActive);
        }

        [Fact]
        public void Deactivate_without_children_deactivates_and_sets_UpdatedAt()
        {
            var university = NewUniversity();

            ResultAssert.Succeeded(university.Deactivate(hasActiveChildren: false, Later));

            Assert.False(university.IsActive);
            Assert.Equal(Later, university.UpdatedAt);
        }

        [Fact]
        public void Deactivate_twice_returns_AlreadyInactive()
        {
            var university = NewUniversity();
            ResultAssert.Succeeded(university.Deactivate(false, Later));

            ResultAssert.Failed(university.Deactivate(false, Later), UniversityErrors.AlreadyInactive);
        }

        [Fact]
        public void Activate_reactivates_and_rejects_when_already_active()
        {
            var university = NewUniversity();

            ResultAssert.Failed(university.Activate(Later), UniversityErrors.AlreadyActive);

            ResultAssert.Succeeded(university.Deactivate(false, Later));
            ResultAssert.Succeeded(university.Activate(Later.AddDays(1)));

            Assert.True(university.IsActive);
            Assert.Equal(Later.AddDays(1), university.UpdatedAt);
        }
    }
}
