namespace UniCore.Domain.Modules.Admissions
{
    // Draft -> Submitted -> UnderReview -> Accepted | ConditionallyAccepted | Rejected
    // Draft / Submitted -> Withdrawn.  Draft / Submitted / UnderReview / ConditionallyAccepted -> Expired (job).
    // ConditionallyAccepted -> Accepted when every mandatory requirement is approved.
    // Stored as text (the AppDbContext convention), so do not rename a value without a migration.
    public enum AdmissionApplicationStatus
    {
        Draft,
        Submitted,
        UnderReview,
        Accepted,
        ConditionallyAccepted,
        Rejected,
        Withdrawn,
        Expired
    }
}
