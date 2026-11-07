using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Scheduling
{
    // A weekly window in which an instructor can teach. An instructor with no windows is available at any time.
    // Windows of the same instructor and day never overlap (checked by the handler).
    public sealed class InstructorAvailability : AggregateRoot
    {
        private InstructorAvailability()
        {
        }

        public Guid InstructorId { get; private set; }
        public DayOfWeek DayOfWeek { get; private set; }
        public TimeOnly StartTime { get; private set; }
        public TimeOnly EndTime { get; private set; }

        public static Result<InstructorAvailability> Create(Guid instructorId, DayOfWeek day, TimeOnly start, TimeOnly end)
        {
            if (instructorId == Guid.Empty)
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

            return new InstructorAvailability { Id = Guid.NewGuid(), InstructorId = instructorId, DayOfWeek = day, StartTime = start, EndTime = end };
        }

        public bool Overlaps(DayOfWeek day, TimeOnly start, TimeOnly end)
            => DayOfWeek == day && TimeRules.Overlaps(StartTime, EndTime, start, end);

        public bool Covers(DayOfWeek day, TimeOnly start, TimeOnly end)
            => DayOfWeek == day && TimeRules.Contains(StartTime, EndTime, start, end);
    }
}
