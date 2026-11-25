using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance;
using UniCore.Application.Modules.StudentServices.Specifications;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.StudentServices;

namespace UniCore.Application.Modules.StudentServices
{
    // A certificate costs a fee (CERTIFICATE in the finance catalog), charged when the request is submitted.
    public sealed class CertificateRequestHandler(
        ServiceRequestAccess access, IAggregateRepository<CertificateRequest> requests, IWorkflowService workflows, IStudentChargeService charges,
        ICurrentUser user, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
        : ServiceRequestHandlerBase<CertificateRequest>(access, requests, workflows, user, unitOfWork, clock, audit)
    {
        private readonly IStudentChargeService _charges = charges;

        protected override string Reference => ServiceRequestKinds.CertificateReference;
        protected override string Definition => ServiceRequestKinds.CertificateDefinition;
        protected override ServiceRequestSummary Map(CertificateRequest request) => ServiceRequestMapper.ToSummary(request);

        public Task<Result<ServiceRequestSummary>> SubmitAsync(Guid studentId, CertificateType? certificateType, string? purpose, RequestInfo info, CancellationToken cancellationToken = default)
        {
            if (certificateType is not { } type)
            {
                return Task.FromResult<Result<ServiceRequestSummary>>(ServiceRequestErrors.Invalid);
            }

            return SubmitCoreAsync(studentId, new OpenCertificateOfStudentSpecification(studentId, type),
                (userId, now) => CertificateRequest.Create(studentId, userId, type, purpose, now),
                async (request, ct) =>
                {
                    var charged = await _charges.ChargeAsync(new ServiceChargeRequest(studentId, ServiceRequestKinds.CertificateFeeCode, $"{Reference}:{request.Id}"), ct);
                    if (!charged.TryGetValue(out var result))
                    {
                        return Result.Fail(charged.Errors);
                    }

                    request.RecordCharge(result.Charge.Id);

                    return Result.Ok();
                }, info, cancellationToken);
        }
    }
}
