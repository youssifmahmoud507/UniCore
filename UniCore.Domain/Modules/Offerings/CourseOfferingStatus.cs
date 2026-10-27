namespace UniCore.Domain.Modules.Offerings
{
    // Draft -> Open <-> Closed, and Cancelled from any other state (final). Registration only accepts students
    // into an Open offering. Stored as text.
    public enum CourseOfferingStatus
    {
        Draft,
        Open,
        Closed,
        Cancelled
    }
}
