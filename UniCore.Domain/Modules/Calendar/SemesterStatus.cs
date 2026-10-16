namespace UniCore.Domain.Modules.Calendar
{
    // Moves forward only. The date windows decide what is allowed; the status is for display and locking.
    public enum SemesterStatus
    {
        Planned = 0,
        RegistrationOpen = 1,
        InProgress = 2,
        Examinations = 3,
        GradingOpen = 4,
        Closed = 5
    }
}
