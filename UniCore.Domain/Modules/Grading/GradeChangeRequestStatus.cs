namespace UniCore.Domain.Modules.Grading
{
    // Pending: the approval workflow is running. Applied: it was approved and the grade now carries the new score.
    // Rejected / Cancelled: the workflow ended without approval. Failed: it was approved but the correction could not be applied
    // (the reason is in FailureCode); nothing changed on the grade.
    public enum GradeChangeRequestStatus
    {
        Pending = 0,
        Applied = 1,
        Rejected = 2,
        Cancelled = 3,
        Failed = 4
    }
}
