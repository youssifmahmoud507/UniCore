namespace UniCore.Domain.Modules.Registrations
{
    // Audit action names of the Registrations module (kept apart from AuditActions so the two developers
    // do not edit the same file).
    public static class RegistrationsAuditActions
    {
        public const string RegistrationSubmitted = "REGISTRATION_SUBMITTED";
        public const string RegistrationCancelled = "REGISTRATION_CANCELLED";
        public const string RegistrationCompleted = "REGISTRATION_COMPLETED";
        public const string EnrollmentRegistered = "ENROLLMENT_REGISTERED";
        public const string EnrollmentDropped = "ENROLLMENT_DROPPED";
        public const string EnrollmentWithdrawn = "ENROLLMENT_WITHDRAWN";

        // The registrar registered a student after the window closed (the reason is in the audit entry).
        public const string RegistrarOverride = "REGISTRATION_REGISTRAR_OVERRIDE";
    }
}
