using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Scheduling
{
    // One meeting of a section: which slot, in which room. It copies the semester, day and times of the slot, so every
    // conflict query reads one table. Slots with placements cannot be removed, so the copy never goes stale.
    // Database backstops: unique (CourseSectionId, ScheduleSlotId) and unique (RoomId, AcademicSemesterId, DayOfWeek,
    // StartTime) - the application check covers partial overlaps.
    public sealed class CourseSectionSchedule : AggregateRoot
    {
        private CourseSectionSchedule()
        {
        }

        public Guid CourseSectionId { get; private set; }
        public Guid ScheduleId { get; private set; }
        public Guid ScheduleSlotId { get; private set; }
        public Guid AcademicSemesterId { get; private set; }
        public Guid RoomId { get; private set; }
        public DayOfWeek DayOfWeek { get; private set; }
        public TimeOnly StartTime { get; private set; }
        public TimeOnly EndTime { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }

        // The application layer checked the section, the room and all the conflict rules.
        public static Result<CourseSectionSchedule> Create(Guid courseSectionId, Schedule schedule, Guid scheduleSlotId, Guid roomId, DateTimeOffset now)
        {
            if (courseSectionId == Guid.Empty || scheduleSlotId == Guid.Empty || roomId == Guid.Empty)
            {
                return SchedulingErrors.AssignmentInvalid;
            }

            var slot = schedule.FindSlot(scheduleSlotId);
            if (slot is null)
            {
                return SchedulingErrors.SlotNotFound;
            }

            var meeting = new CourseSectionSchedule
            {
                Id = Guid.NewGuid(),
                CourseSectionId = courseSectionId,
                ScheduleId = schedule.Id,
                ScheduleSlotId = slot.Id,
                AcademicSemesterId = schedule.AcademicSemesterId,
                RoomId = roomId,
                DayOfWeek = slot.DayOfWeek,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                CreatedAt = now
            };

            meeting.RaiseDomainEvent(new SectionScheduled(Guid.NewGuid(), now, meeting.Id, courseSectionId, roomId, slot.DayOfWeek, slot.StartTime, slot.EndTime));
            return meeting;
        }

        public bool Overlaps(DayOfWeek day, TimeOnly start, TimeOnly end)
            => DayOfWeek == day && TimeRules.Overlaps(StartTime, EndTime, start, end);
    }
}
