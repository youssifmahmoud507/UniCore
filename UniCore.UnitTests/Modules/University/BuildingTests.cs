using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.University
{
    public class BuildingTests
    {
        private static readonly Guid CampusId = Guid.NewGuid();

        private static Building NewBuilding() => ResultAssert.Succeeded(Building.Create(CampusId, "eng-a", "Engineering A"));

        [Fact]
        public void Create_with_valid_data_normalizes_code_and_starts_active()
        {
            var building = ResultAssert.Succeeded(Building.Create(CampusId, " eng-a ", " Engineering A "));

            Assert.NotEqual(Guid.Empty, building.Id);
            Assert.Equal(CampusId, building.CampusId);
            Assert.Equal("ENG-A", building.Code);
            Assert.Equal("Engineering A", building.Name);
            Assert.True(building.IsActive);
        }

        [Fact]
        public void Create_rejects_empty_campus_id()
        {
            ResultAssert.Failed(Building.Create(Guid.Empty, "ENG-A", "Engineering A"), UniversityErrors.InvalidParent);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("E")]
        [InlineData("ENG A")]
        public void Create_rejects_invalid_code(string? code)
        {
            ResultAssert.Failed(Building.Create(CampusId, code, "Engineering A"), UniversityErrors.InvalidCode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" ")]
        [InlineData("E")]
        public void Create_rejects_invalid_name(string? name)
        {
            ResultAssert.Failed(Building.Create(CampusId, "ENG-A", name), UniversityErrors.InvalidName);
        }

        [Fact]
        public void Rename_changes_name_but_never_the_code()
        {
            var building = NewBuilding();

            ResultAssert.Succeeded(building.Rename("Engineering Block A"));

            Assert.Equal("Engineering Block A", building.Name);
            Assert.Equal("ENG-A", building.Code);
            ResultAssert.Failed(building.Rename(" "), UniversityErrors.InvalidName);
            Assert.Equal("Engineering Block A", building.Name);
        }

        [Fact]
        public void Deactivate_with_active_rooms_is_rejected()
        {
            var building = NewBuilding();

            ResultAssert.Failed(building.Deactivate(hasActiveRooms: true), UniversityErrors.BuildingHasActiveRooms);
            Assert.True(building.IsActive);
        }

        [Fact]
        public void Deactivate_and_Activate_follow_the_state_rules()
        {
            var building = NewBuilding();

            ResultAssert.Failed(building.Activate(), UniversityErrors.AlreadyActive);
            ResultAssert.Succeeded(building.Deactivate(false));
            Assert.False(building.IsActive);

            ResultAssert.Failed(building.Deactivate(false), UniversityErrors.AlreadyInactive);
            ResultAssert.Succeeded(building.Activate());
            Assert.True(building.IsActive);
        }
    }
}
