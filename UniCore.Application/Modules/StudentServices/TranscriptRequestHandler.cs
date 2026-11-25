using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance;
using UniCore.Application.Modules.StudentServices.Specifications;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.StudentServices;

namespace UniCore.Application.Modules.StudentServices
{
    // An official transcript: fee TRANSCRIPT at submit; the snapshot is issued when the workflow approves (see ServiceRequestWorkflowReactions).
    public sealed class TranscriptRequestHandler(
        ServiceRequestAccess access, IAggregateRepository<TranscriptRequest> requests, IWorkflowService workflows, IStudentChargeService charges,
        ICurrentUser user, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
        : ServiceRequestHandlerBase<TranscriptRequest>(access, requests, workflows, user, unitOfWork, clock, audit)
    {
        private readonly IStudentChargeService _charges = charges;

        protected override string Reference => ServiceRequestKinds.TranscriptReference;
        protected override string Definition => ServiceRequestKinds.TranscriptDefinition;
        protected override ServiceRequestSummary Map(TranscriptRequest request) => ServiceRequestMapper.ToSummary(request);

        public Task<Result<ServiceRequestSummary>> SubmitAsync(Guid studentId, string? purpose, RequestInfo info, CancellationToken cancellationToken = default)
            => SubmitCoreAsync(studentId, new OpenTranscriptOfStudentSpecification(studentId),
                (userId, now) => TranscriptRequest.Create(studentId, userId, purpose, now),
                async (request, ct) =>
                {
                    var charged = await _charges.ChargeAsync(new ServiceChargeRequest(studentId, ServiceRequestKinds.TranscriptFeeCode, $"{Reference}:{request.Id}"), ct);
                    if (!charged.TryGetValue(out var result))
                    {
                        return Result.Fail(charged.Errors);
                    }

                    request.RecordCharge(result.Charge.Id);

                    return Result.Ok();
                }, info, cancellationToken);
    }
}
