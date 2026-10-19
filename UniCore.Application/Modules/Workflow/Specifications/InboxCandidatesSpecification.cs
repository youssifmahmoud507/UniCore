using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow.Specifications
{
    // In-progress instances whose current step needs one of these roles, oldest first. The scope is checked afterwards.
    public sealed class InboxCandidatesSpecification : Specification<WorkflowInstance>
    {
        public InboxCandidatesSpecification(PaginationQuery query, string[] roles)
        {
            SetCriteria(i => i.Status == WorkflowInstanceStatus.InProgress && i.CurrentRole != null && roles.Contains(i.CurrentRole));

            ApplyOrderBy(i => i.StartedAt);
            ApplyPaging(query);
        }
    }
}
