using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Scheduling
{
    // A weekly time band of a schedule ("Sunday 09:00-10:30"). Created and removed only through Schedule. Once a section
    // is placed in it, the placement copies the day and times, so a slot is never edited, only removed (when unused).
    public sealed class ScheduleSlot : BaseEntity
    {
        private ScheduleSlot()
        {
        }

        public Guid ScheduleId { get; private set; }
        public DayOfWeek DayOfWeek { get; private set; }
        public TimeOnly StartTime { get; private set; }
        public TimeOnly EndTime { get; private set; }

        internal static ScheduleSlot Create(Guid scheduleId, DayOfWeek day, TimeOnly start, TimeOnly end)
            => new() { Id = Guid.NewGuid(), ScheduleId = scheduleId, DayOfWeek = day, StartTime = start, EndTime = end };

        public bool Overlaps(DayOfWeek day, TimeOnly start, TimeOnly end)
            => DayOfWeek == day && TimeRules.Overlaps(StartTime, EndTime, start, end);
    }
}
