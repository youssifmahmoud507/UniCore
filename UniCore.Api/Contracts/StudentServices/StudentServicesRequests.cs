using UniCore.Domain.Modules.StudentServices;

namespace UniCore.Api.Contracts.StudentServices
{
    public sealed record SubmitCertificateRequest(CertificateType? CertificateType, string? Purpose);

    public sealed record SubmitTranscriptRequest(string? Purpose);

    public sealed record SubmitComplaintRequest(ComplaintCategory? Category, string? Subject, string? Details);

    public sealed record SubmitAppealRequest(AppealTarget? Target, string? Subject, string? Reason);
}
