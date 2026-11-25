using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.StudentServices
{
    // A student asks the registrar for a certificate. One open request per student and certificate type (a unique index enforces it).
    public sealed class CertificateRequest : ServiceRequest
    {
        private CertificateRequest()
        {
        }

        public CertificateType CertificateType { get; private set; }
        public string Purpose { get; private set; } = string.Empty;
        public Guid? ChargeId { get; private set; }

        public static Result<CertificateRequest> Create(Guid studentId, Guid requestedByUserId, CertificateType certificateType, string? purpose, DateTimeOffset now)
        {
            if (!Enum.IsDefined(certificateType))
            {
                return ServiceRequestErrors.Invalid;
            }

            var text = RequiredText(purpose, ShortTextMaxLength);
            if (!text.TryGetValue(out var validPurpose))
            {
                return Result<CertificateRequest>.Fail(text.Errors);
            }

            var request = new CertificateRequest { CertificateType = certificateType, Purpose = validPurpose };
            var started = request.Initialize(studentId, requestedByUserId, now);

            return started.IsFailure ? Result<CertificateRequest>.Fail(started.Errors) : request;
        }

        // The charge the finance module made for the certificate (idempotent by this request's id).
        public void RecordCharge(Guid chargeId) => ChargeId = chargeId;
    }
}
