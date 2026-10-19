using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Workflow.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow
{
    public sealed class WorkflowQueryHandler(IPermissionAuthorizer authorizer, ICurrentUser user, WorkflowApproverCheck approvers, IAggregateRepository<WorkflowInstance> instances)
    {
        // The inbox looks at the oldest items first, in pages of 100, and stops after this many pages.
        private const int MaxInboxPages = 10;

        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly WorkflowApproverCheck _approvers = approvers;
        private readonly IAggregateRepository<WorkflowInstance> _instances = instances;

        // Staff with a Global scope see every instance.
        public async Task<Result<PagedResult<WorkflowInstanceSummaryDto>>> ListAsync(PaginationQuery query, WorkflowInstanceStatus? status, string? referenceType, Guid? referenceId, Guid? subjectStudentId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Requests.Read, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<WorkflowInstanceSummaryDto>>.Fail(auth.Errors);
            }

            return await PageAsync(new WorkflowInstancesListSpecification(query, status, null, NullIfBlank(referenceType), referenceId, subjectStudentId), query, cancellationToken);
        }

        // The requests the caller made.
        public async Task<Result<PagedResult<WorkflowInstanceSummaryDto>>> MineAsync(PaginationQuery query, WorkflowInstanceStatus? status, CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var auth = await _authorizer.AuthorizeAsync(Permissions.Requests.Read, ResourceContext.OwnedBy(userId), cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<WorkflowInstanceSummaryDto>>.Fail(auth.Errors);
            }

            return await PageAsync(new WorkflowInstancesListSpecification(query, status, userId, null, null, null), query, cancellationToken);
        }

        // What waits for this user: in progress, the current step needs a role they hold, and the scope covers the request.
        public async Task<Result<PagedResult<WorkflowInstanceSummaryDto>>> InboxAsync(PaginationQuery query, CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var roles = _approvers.ApproverRoles();
            var visible = new List<WorkflowInstanceSummaryDto>();

            for (var page = 1; page <= MaxInboxPages && roles.Count > 0; page++)
            {
                var batch = await _instances.ListAsync(new InboxCandidatesSpecification(new PaginationQuery(page, PaginationQuery.MaxPageSize), [.. roles]), cancellationToken);

                foreach (var instance in batch)
                {
                    // Nobody decides their own request, so it is not in their inbox.
                    if (instance.RequesterUserId == userId || instance.CurrentRole is not { } role)
                    {
                        continue;
                    }

                    if (await _approvers.CanActAsync(role, instance.ScopeType, instance.ScopeId, cancellationToken))
                    {
                        visible.Add(WorkflowMapper.ToSummary(instance));
                    }
                }

                if (batch.Count < PaginationQuery.MaxPageSize)
                {
                    break;
                }
            }

            var items = visible.Skip(query.Skip).Take(query.NormalizedPageSize).ToList();

            return PagedResult<WorkflowInstanceSummaryDto>.Create(items, query, visible.Count);
        }

        private async Task<Result<PagedResult<WorkflowInstanceSummaryDto>>> PageAsync(WorkflowInstancesListSpecification specification, PaginationQuery query, CancellationToken cancellationToken)
        {
            var items = await _instances.ListAsync(specification, cancellationToken);
            var total = await _instances.CountAsync(specification, cancellationToken);

            return PagedResult<WorkflowInstanceSummaryDto>.Create(items.Select(WorkflowMapper.ToSummary).ToList(), query, total);
        }

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }



}
