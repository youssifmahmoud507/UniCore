namespace UniCore.Domain.Modules.Scheduling
{
    // Draft -> Published -> Archived. Archived is read-only. Stored as text.
    public enum ScheduleStatus
    {
        Draft,
        Published,
        Archived
    }
}
