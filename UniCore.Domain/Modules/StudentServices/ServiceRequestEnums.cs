namespace UniCore.Domain.Modules.StudentServices
{
    // Pending: in the workflow. Returned: sent back to the student for a correction. Approved, Rejected and Cancelled are final.
    public enum ServiceRequestStatus
    {
        Pending = 0,
        Returned = 1,
        Approved = 2,
        Rejected = 3,
        Cancelled = 4
    }

    public enum CertificateType
    {
        EnrollmentProof = 0,
        GoodStanding = 1,
        Graduation = 2
    }

    public enum ComplaintCategory
    {
        Academic = 0,
        Administrative = 1,
        Facilities = 2,
        Other = 3
    }

    // What an appeal is against.
    public enum AppealTarget
    {
        AcademicStanding = 0,
        Dismissal = 1,
        Disciplinary = 2,
        Other = 3
    }
}
