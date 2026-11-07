using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Scheduling
{
    // A weekly window in which a room can be used. A room with no windows is available at any time.
    // Windows of the same room and day never overlap (checked by the handler).
    public sealed class RoomAvailability : AggregateRoot
    {
        private RoomAvailability()
        {
        }

        public Guid RoomId { get; private set; }
        public DayOfWeek DayOfWeek { get; private set; }
        public TimeOnly StartTime { get; private set; }
        public TimeOnly EndTime { get; private set; }

        public static Result<RoomAvailability> Create(Guid roomId, DayOfWeek day, TimeOnly start, TimeOnly end)
        {
            if (roomId == Guid.Empty)
            {
                return SchedulingErrors.AvailabilityInvalid;
            }

            if (!Enum.IsDefined(day))
            {
                return SchedulingErrors.DayInvalid;
            }

            if (!TimeRules.IsValidWindow(start, end))
            {
                return SchedulingErrors.TimeInvalid;
            }

            return new RoomAvailability { Id = Guid.NewGuid(), RoomId = roomId, DayOfWeek = day, StartTime = start, EndTime = end };
        }

        public bool Overlaps(DayOfWeek day, TimeOnly start, TimeOnly end)
            => DayOfWeek == day && TimeRules.Overlaps(StartTime, EndTime, start, end);

        public bool Covers(DayOfWeek day, TimeOnly start, TimeOnly end)
            => DayOfWeek == day && TimeRules.Contains(StartTime, EndTime, start, end);
    }
}
