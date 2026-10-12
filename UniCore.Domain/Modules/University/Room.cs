using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.University
{
    // A room in a building. Scheduling and exams will reference it by RoomId.
    // The data model has no timestamps for rooms.
    public sealed class Room : AggregateRoot
    {
        public const int RoomNumberMinLength = 1;
        public const int RoomNumberMaxLength = 20;
        public const int MaxCapacity = 10000;

        private Room()
        {
        }

        public Guid BuildingId { get; private set; }

        // Unique inside the building (database index on BuildingId + RoomNumber). Upper-case, never changed.
        public string RoomNumber { get; private set; } = string.Empty;

        public string? Name { get; private set; }
        public int Capacity { get; private set; }
        public RoomType RoomType { get; private set; }
        public bool IsActive { get; private set; }

        // The application layer must check that the building exists and is active before calling this.
        public static Result<Room> Create(Guid buildingId, string? roomNumber, string? name, int capacity, RoomType roomType)
        {
            if (buildingId == Guid.Empty)
            {
                return UniversityErrors.InvalidParent;
            }

            var cleanNumber = roomNumber?.Trim().ToUpperInvariant();

            if (cleanNumber is null || !FieldRules.IsValidCode(cleanNumber, RoomNumberMinLength, RoomNumberMaxLength))
            {
                return UniversityErrors.InvalidCode;
            }

            var room = new Room
            {
                Id = Guid.NewGuid(),
                BuildingId = buildingId,
                RoomNumber = cleanNumber,
                IsActive = true
            };

            var details = room.ApplyDetails(name, capacity, roomType);

            if (details.IsFailure)
            {
                return Result<Room>.Fail(details.Errors);
            }

            return room;
        }

        public Result Update(string? name, int capacity, RoomType roomType)
        {
            return ApplyDetails(name, capacity, roomType);
        }

        public Result Activate()
        {
            if (IsActive)
            {
                return Result.Fail(UniversityErrors.AlreadyActive);
            }

            IsActive = true;
            return Result.Ok();
        }

        public Result Deactivate()
        {
            if (!IsActive)
            {
                return Result.Fail(UniversityErrors.AlreadyInactive);
            }

            IsActive = false;
            return Result.Ok();
        }

        private Result ApplyDetails(string? name, int capacity, RoomType roomType)
        {
            var cleanName = FieldRules.NormalizeOptional(name);
            if (cleanName is not null && cleanName.Length > FieldRules.NameMaxLength)
            {
                return Result.Fail(UniversityErrors.InvalidName);
            }

            if (capacity < 1 || capacity > MaxCapacity)
            {
                return Result.Fail(UniversityErrors.InvalidRoomCapacity);
            }

            if (!Enum.IsDefined(roomType))
            {
                return Result.Fail(UniversityErrors.InvalidRoomType);
            }

            Name = cleanName;
            Capacity = capacity;
            RoomType = roomType;
            return Result.Ok();
        }
    }
}
