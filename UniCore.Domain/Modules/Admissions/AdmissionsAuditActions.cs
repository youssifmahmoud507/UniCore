namespace UniCore.Domain.Modules.Admissions
{
    // Audit action names of the Admissions module (kept apart from AuditActions so the two developers
    // do not edit the same file). Audit values never contain email, phone, date of birth or document content.
    public static class AdmissionsAuditActions
    {
        public const string ApplicantCreated = "APPLICANT_CREATED";
        public const string ApplicantUpdated = "APPLICANT_UPDATED";

        public const string RequirementCreated = "ADMISSION_REQUIREMENT_CREATED";
        public const string RequirementUpdated = "ADMISSION_REQUIREMENT_UPDATED";
        public const string RequirementActivated = "ADMISSION_REQUIREMENT_ACTIVATED";
        public const string RequirementDeactivated = "ADMISSION_REQUIREMENT_DEACTIVATED";

        public const string ApplicationCreated = "ADMISSION_APPLICATION_CREATED";
        public const string DocumentAttached = "ADMISSION_DOCUMENT_ATTACHED";
        public const string ApplicationSubmitted = "ADMISSION_APPLICATION_SUBMITTED";
        public const string ReviewStarted = "ADMISSION_REVIEW_STARTED";
        public const string RequirementApproved = "ADMISSION_REQUIREMENT_APPROVED";
        public const string RequirementRejected = "ADMISSION_REQUIREMENT_REJECTED";
        public const string ApplicationAccepted = "ADMISSION_ACCEPTED";
        public const string ApplicationConditionallyAccepted = "ADMISSION_CONDITIONALLY_ACCEPTED";
        public const string ApplicationRejected = "ADMISSION_REJECTED";
        public const string ApplicationWithdrawn = "ADMISSION_WITHDRAWN";
        public const string ApplicationExpired = "ADMISSION_EXPIRED";
    }
}
