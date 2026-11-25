namespace UniCore.Domain.Modules.StudentServices
{
    // The names used with the workflow engine: the definition to start (seeded by the engine) and the ReferenceType the
    // engine puts back on its events, so each request module recognises its own.
    public static class ServiceRequestKinds
    {
        public const string CertificateReference = "CertificateRequest";
        public const string TranscriptReference = "TranscriptRequest";
        public const string ComplaintReference = "Complaint";
        public const string AppealReference = "Appeal";

        public const string CertificateDefinition = "CERTIFICATE_REQUEST";
        public const string TranscriptDefinition = "TRANSCRIPT_REQUEST";
        public const string ComplaintDefinition = "COMPLAINT";
        public const string AppealDefinition = "APPEAL";

        // Codes of the fees in the finance catalog (Developer 2 seeds or creates them).
        public const string CertificateFeeCode = "CERTIFICATE";
        public const string TranscriptFeeCode = "TRANSCRIPT";
    }
}
