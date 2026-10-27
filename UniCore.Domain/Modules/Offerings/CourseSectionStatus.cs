namespace UniCore.Domain.Modules.Offerings
{
    // Open <-> Closed, and Cancelled from either (final, only when nobody is enrolled). Stored as text.
    public enum CourseSectionStatus
    {
        Open,
        Closed,
        Cancelled
    }
}
