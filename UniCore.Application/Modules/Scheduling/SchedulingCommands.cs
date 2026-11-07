namespace UniCore.Application.Modules.Scheduling
{
    public sealed record CreateScheduleCommand(Guid AcademicSemesterId, string? Name);

    public sealed record UpdateScheduleCommand(string? Name);

    public sealed record AddSlotCommand(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

    public sealed record AssignSectionCommand(Guid CourseSectionId, Guid ScheduleSlotId, Guid RoomId);

    public sealed record AddAvailabilityCommand(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
}
