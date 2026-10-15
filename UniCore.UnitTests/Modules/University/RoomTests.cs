using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.University
{
    public class RoomTests
    {
        private static readonly Guid BuildingId = Guid.NewGuid();

        private static Room NewRoom() => ResultAssert.Succeeded(Room.Create(BuildingId, "a-101", "Lecture hall 1", 120, RoomType.LectureHall));

        [Fact]
        public void Create_with_valid_data_normalizes_room_number_and_starts_active()
        {
            var room = ResultAssert.Succeeded(Room.Create(BuildingId, " a-101 ", " Lecture hall 1 ", 120, RoomType.LectureHall));

            Assert.NotEqual(Guid.Empty, room.Id);
            Assert.Equal(BuildingId, room.BuildingId);
            Assert.Equal("A-101", room.RoomNumber);
            Assert.Equal("Lecture hall 1", room.Name);
            Assert.Equal(120, room.Capacity);
            Assert.Equal(RoomType.LectureHall, room.RoomType);
            Assert.True(room.IsActive);
        }

        [Fact]
        public void Create_allows_a_one_character_room_number_and_a_missing_name()
        {
            var room = ResultAssert.Succeeded(Room.Create(BuildingId, "7", null, 30, RoomType.Classroom));

            Assert.Equal("7", room.RoomNumber);
            Assert.Null(room.Name);
        }

        [Fact]
        public void Create_rejects_empty_building_id()
        {
            ResultAssert.Failed(Room.Create(Guid.Empty, "A-101", null, 30, RoomType.Classroom), UniversityErrors.InvalidParent);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("A 101")]
        [InlineData("A.101")]
        [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
        public void Create_rejects_invalid_room_number(string? number)
        {
            ResultAssert.Failed(Room.Create(BuildingId, number, null, 30, RoomType.Classroom), UniversityErrors.InvalidCode);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(10001)]
        public void Create_rejects_capacity_outside_1_to_10000(int capacity)
        {
            ResultAssert.Failed(Room.Create(BuildingId, "A-101", null, capacity, RoomType.Classroom), UniversityErrors.InvalidRoomCapacity);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10000)]
        public void Create_accepts_the_capacity_boundaries(int capacity)
        {
            var room = ResultAssert.Succeeded(Room.Create(BuildingId, "A-101", null, capacity, RoomType.Auditorium));

            Assert.Equal(capacity, room.Capacity);
        }

        [Fact]
        public void Create_rejects_an_undefined_room_type_and_a_too_long_name()
        {
            ResultAssert.Failed(Room.Create(BuildingId, "A-101", null, 30, (RoomType)999), UniversityErrors.InvalidRoomType);
            ResultAssert.Failed(Room.Create(BuildingId, "A-101", new string('a', 201), 30, RoomType.Classroom), UniversityErrors.InvalidName);
        }

        [Fact]
        public void Update_changes_name_capacity_and_type_but_never_the_room_number()
        {
            var room = NewRoom();

            ResultAssert.Succeeded(room.Update(null, 40, RoomType.Laboratory));

            Assert.Null(room.Name);
            Assert.Equal(40, room.Capacity);
            Assert.Equal(RoomType.Laboratory, room.RoomType);
            Assert.Equal("A-101", room.RoomNumber);
        }

        [Fact]
        public void Update_with_invalid_data_changes_nothing()
        {
            var room = NewRoom();

            ResultAssert.Failed(room.Update("New name", 0, RoomType.Laboratory), UniversityErrors.InvalidRoomCapacity);

            Assert.Equal("Lecture hall 1", room.Name);
            Assert.Equal(120, room.Capacity);
            Assert.Equal(RoomType.LectureHall, room.RoomType);
        }

        [Fact]
        public void Deactivate_and_Activate_follow_the_state_rules()
        {
            var room = NewRoom();

            ResultAssert.Failed(room.Activate(), UniversityErrors.AlreadyActive);
            ResultAssert.Succeeded(room.Deactivate());
            Assert.False(room.IsActive);

            ResultAssert.Failed(room.Deactivate(), UniversityErrors.AlreadyInactive);
            ResultAssert.Succeeded(room.Activate());
            Assert.True(room.IsActive);
        }
    }
}
