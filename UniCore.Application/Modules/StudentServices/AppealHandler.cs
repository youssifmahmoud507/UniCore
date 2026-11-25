using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance;
using UniCore.Application.Modules.StudentServices.Specifications;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.StudentServices;

namespace UniCore.Application.Modules.StudentServices
{
    // An appeal against an academic or disciplinary decision (not a grade: grade appeals are Part 2).
    public sealed class AppealHandler(
        ServiceRequestAccess access, IAggregateRepository<Appeal> requests, IWorkflowService workflows,
        ICurrentUser user, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
        : ServiceRequestHandlerBase<Appeal>(access, requests, workflows, user, unitOfWork, clock, audit)
    {
        protected override string Reference => ServiceRequestKinds.AppealReference;
        protected override string Definition => ServiceRequestKinds.AppealDefinition;
        protected override ServiceRequestSummary Map(Appeal request) => ServiceRequestMapper.ToSummary(request);

        public Task<Result<ServiceRequestSummary>> SubmitAsync(Guid studentId, AppealTarget? target, string? subject, string? reason, RequestInfo info, CancellationToken cancellationToken = default)
        {
            if (target is not { } value)
            {
                return Task.FromResult<Result<ServiceRequestSummary>>(ServiceRequestErrors.Invalid);
            }

            return SubmitCoreAsync(studentId, null, (userId, now) => Appeal.Create(studentId, userId, value, subject, reason, now), null, info, cancellationToken);
        }
    }
}
