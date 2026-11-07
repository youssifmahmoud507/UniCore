namespace UniCore.Domain.Modules.Registrations
{
    // Registered -> Dropped | Withdrawn | Completed. Pass or fail belongs to the grade, not to this status.
    // Dropped and Withdrawn are final and keep their row (history). Stored as text.
    public enum EnrollmentStatus
    {
        Registered,
        Dropped,
        Withdrawn,
        Completed
    }
}
