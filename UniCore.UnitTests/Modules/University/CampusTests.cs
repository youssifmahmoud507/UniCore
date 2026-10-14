using UniCore.Domain.Common.ValueObjects;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.University
{
    public class CampusTests
    {
        private static readonly Guid UniversityId = Guid.NewGuid();

        private static Address NewAddress() => ResultAssert.Succeeded(Address.Create("1 Gamaa St", "Giza", null, "12613", "Egypt"));

        private static Campus NewCampus() => ResultAssert.Succeeded(Campus.Create(UniversityId, "main", "Main Campus", NewAddress(), "Africa/Cairo"));

        [Fact]
        public void Create_with_valid_data_normalizes_code_and_starts_active()
        {
            var campus = ResultAssert.Succeeded(Campus.Create(UniversityId, " main ", " Main Campus ", NewAddress(), " Africa/Cairo "));

            Assert.NotEqual(Guid.Empty, campus.Id);
            Assert.Equal(UniversityId, campus.UniversityId);
            Assert.Equal("MAIN", campus.Code);
            Assert.Equal("Main Campus", campus.Name);
            Assert.Equal(NewAddress(), campus.Address);
            Assert.Equal("Africa/Cairo", campus.TimeZone);
            Assert.True(campus.IsActive);
        }

        [Fact]
        public void Create_rejects_empty_university_id()
        {
            ResultAssert.Failed(Campus.Create(Guid.Empty, "MAIN", "Main Campus", NewAddress(), "UTC"), UniversityErrors.InvalidParent);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("M")]
        [InlineData("MA IN")]
        public void Create_rejects_invalid_code(string? code)
        {
            ResultAssert.Failed(Campus.Create(UniversityId, code, "Main Campus", NewAddress(), "UTC"), UniversityErrors.InvalidCode);
        }

        [Fact]
        public void Create_rejects_invalid_name_missing_address_and_invalid_time_zone()
        {
            ResultAssert.Failed(Campus.Create(UniversityId, "MAIN", " ", NewAddress(), "UTC"), UniversityErrors.InvalidName);
            ResultAssert.Failed(Campus.Create(UniversityId, "MAIN", "Main Campus", null, "UTC"), UniversityErrors.AddressRequired);
            ResultAssert.Failed(Campus.Create(UniversityId, "MAIN", "Main Campus", NewAddress(), null), UniversityErrors.InvalidTimeZone);
            ResultAssert.Failed(Campus.Create(UniversityId, "MAIN", "Main Campus", NewAddress(), "Africa Cairo"), UniversityErrors.InvalidTimeZone);
        }

        [Fact]
        public void Update_changes_name_address_and_time_zone_but_never_the_code()
        {
            var campus = NewCampus();
            var newAddress = ResultAssert.Succeeded(Address.Create("5 Nile St", "Cairo", null, null, "Egypt"));

            ResultAssert.Succeeded(campus.Update("North Campus", newAddress, "UTC"));

            Assert.Equal("North Campus", campus.Name);
            Assert.Equal(newAddress, campus.Address);
            Assert.Equal("UTC", campus.TimeZone);
            Assert.Equal("MAIN", campus.Code);
        }

        [Fact]
        public void Update_with_invalid_data_changes_nothing()
        {
            var campus = NewCampus();

            ResultAssert.Failed(campus.Update("North Campus", NewAddress(), "bad zone"), UniversityErrors.InvalidTimeZone);

            Assert.Equal("Main Campus", campus.Name);
            Assert.Equal("Africa/Cairo", campus.TimeZone);
        }

        [Fact]
        public void Deactivate_with_active_buildings_is_rejected()
        {
            var campus = NewCampus();

            ResultAssert.Failed(campus.Deactivate(hasActiveBuildings: true), UniversityErrors.CampusHasActiveBuildings);
            Assert.True(campus.IsActive);
        }

        [Fact]
        public void Deactivate_and_Activate_follow_the_state_rules()
        {
            var campus = NewCampus();

            ResultAssert.Failed(campus.Activate(), UniversityErrors.AlreadyActive);
            ResultAssert.Succeeded(campus.Deactivate(false));
            Assert.False(campus.IsActive);

            ResultAssert.Failed(campus.Deactivate(false), UniversityErrors.AlreadyInactive);
            ResultAssert.Succeeded(campus.Activate());
            Assert.True(campus.IsActive);
        }
    }
}
