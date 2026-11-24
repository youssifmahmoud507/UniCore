namespace UniCore.Domain.Modules.StudentRequests
{
    // Pending -> Applied | Rejected | Cancelled | Failed. Stored as text.
    public enum AcademicRequestStatus
    {
        Pending,
        Applied,
        Rejected,
        Cancelled,
        Failed
    }
}
