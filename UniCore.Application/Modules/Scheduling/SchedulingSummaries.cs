using UniCore.Domain.Modules.Scheduling;

namespace UniCore.Application.Modules.Scheduling
{
    public sealed record SlotSummary(Guid Id, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

    public sealed record ScheduleSummary(Guid Id, Guid AcademicSemesterId, string Name, ScheduleStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, IReadOnlyList<SlotSummary> Slots);

    public sealed record MeetingSummary(Guid Id, Guid CourseSectionId, Guid ScheduleId, Guid ScheduleSlotId, Guid RoomId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

    public sealed record AvailabilitySummary(Guid Id, Guid OwnerId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

    public static class SchedulingMapper
    {
        public static ScheduleSummary ToSummary(Schedule s)
            => new(s.Id, s.AcademicSemesterId, s.Name, s.Status, s.CreatedAt, s.UpdatedAt,
                [.. s.Slots.OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime).Select(x => new SlotSummary(x.Id, x.DayOfWeek, x.StartTime, x.EndTime))]);

        public static MeetingSummary ToSummary(CourseSectionSchedule m)
            => new(m.Id, m.CourseSectionId, m.ScheduleId, m.ScheduleSlotId, m.RoomId, m.DayOfWeek, m.StartTime, m.EndTime);

        public static AvailabilitySummary ToSummary(InstructorAvailability a) => new(a.Id, a.InstructorId, a.DayOfWeek, a.StartTime, a.EndTime);

        public static AvailabilitySummary ToSummary(RoomAvailability a) => new(a.Id, a.RoomId, a.DayOfWeek, a.StartTime, a.EndTime);
    }
}
