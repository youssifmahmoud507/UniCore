using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance;
using UniCore.Application.Modules.StudentServices.Specifications;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.StudentServices;

namespace UniCore.Application.Modules.StudentServices
{
    // A complaint is free and a student may have several open at once.
    public sealed class ComplaintHandler(
        ServiceRequestAccess access, IAggregateRepository<Complaint> requests, IWorkflowService workflows,
        ICurrentUser user, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
        : ServiceRequestHandlerBase<Complaint>(access, requests, workflows, user, unitOfWork, clock, audit)
    {
        protected override string Reference => ServiceRequestKinds.ComplaintReference;
        protected override string Definition => ServiceRequestKinds.ComplaintDefinition;
        protected override ServiceRequestSummary Map(Complaint request) => ServiceRequestMapper.ToSummary(request);

        public Task<Result<ServiceRequestSummary>> SubmitAsync(Guid studentId, ComplaintCategory? category, string? subject, string? details, RequestInfo info, CancellationToken cancellationToken = default)
        {
            if (category is not { } value)
            {
                return Task.FromResult<Result<ServiceRequestSummary>>(ServiceRequestErrors.Invalid);
            }

            return SubmitCoreAsync(studentId, null, (userId, now) => Complaint.Create(studentId, userId, value, subject, details, now), null, info, cancellationToken);
        }
    }
}
