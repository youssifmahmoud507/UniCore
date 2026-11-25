using UniCore.Domain.Modules.StudentServices;

namespace UniCore.Application.Modules.StudentServices
{
    // One shape for all kinds: the shared fields, and the kind's own fields in Details (name -> text).
    public sealed record ServiceRequestSummary(
        Guid Id, string Kind, Guid StudentId, ServiceRequestStatus Status, Guid? WorkflowInstanceId, DateTimeOffset SubmittedAt, DateTimeOffset UpdatedAt,
        DateTimeOffset? DecidedAt, Guid? DecidedByUserId, string? DecisionNote, IReadOnlyDictionary<string, string?> Details);

    public static class ServiceRequestMapper
    {
        public static ServiceRequestSummary ToSummary(CertificateRequest r) => Build(r, ServiceRequestKinds.CertificateReference, new Dictionary<string, string?>
        {
            ["certificateType"] = r.CertificateType.ToString(), ["purpose"] = r.Purpose, ["chargeId"] = r.ChargeId?.ToString(),
        });

        public static ServiceRequestSummary ToSummary(TranscriptRequest r) => Build(r, ServiceRequestKinds.TranscriptReference, new Dictionary<string, string?>
        {
            ["purpose"] = r.Purpose, ["chargeId"] = r.ChargeId?.ToString(), ["transcriptSnapshotId"] = r.TranscriptSnapshotId?.ToString(),
        });

        public static ServiceRequestSummary ToSummary(Complaint r) => Build(r, ServiceRequestKinds.ComplaintReference, new Dictionary<string, string?>
        {
            ["category"] = r.Category.ToString(), ["subject"] = r.Subject, ["details"] = r.Details,
        });

        public static ServiceRequestSummary ToSummary(Appeal r) => Build(r, ServiceRequestKinds.AppealReference, new Dictionary<string, string?>
        {
            ["target"] = r.Target.ToString(), ["subject"] = r.Subject, ["reason"] = r.Reason,
        });

        private static ServiceRequestSummary Build(ServiceRequest r, string kind, Dictionary<string, string?> details)
            => new(r.Id, kind, r.StudentId, r.Status, r.WorkflowInstanceId, r.SubmittedAt, r.UpdatedAt, r.DecidedAt, r.DecidedByUserId, r.DecisionNote, details);
    }
}
