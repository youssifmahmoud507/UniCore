namespace UniCore.Api.Contracts.Scheduling
{
    // Ids, days and times are nullable so a missing value is reported as a validation error instead of silently becoming a default.
    // Times are "HH:mm" or "HH:mm:ss".
    public sealed record CreateScheduleRequest(Guid? AcademicSemesterId, string? Name);
    public sealed record UpdateScheduleRequest(string? Name);

    public sealed record AddSlotRequest(DayOfWeek? DayOfWeek, TimeOnly? StartTime, TimeOnly? EndTime);

    public sealed record AssignSectionRequest(Guid? CourseSectionId, Guid? ScheduleSlotId, Guid? RoomId);

    public sealed record AddAvailabilityRequest(DayOfWeek? DayOfWeek, TimeOnly? StartTime, TimeOnly? EndTime);
}
