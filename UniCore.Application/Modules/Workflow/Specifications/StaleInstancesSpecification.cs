using System.Linq.Expressions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow.Specifications
{
    public sealed class StaleInstancesSpecification : Specification<WorkflowInstance>
    {
        public StaleInstancesSpecification(DateTimeOffset cutoff, PaginationQuery page)
        {
            SetCriteria(Open(cutoff));
            ApplyOrderBy(i => i.Id);
            ApplyPaging(page);
        }

        internal static Expression<Func<WorkflowInstance, bool>> Open(DateTimeOffset cutoff)
            => i => (i.Status == WorkflowInstanceStatus.InProgress || i.Status == WorkflowInstanceStatus.Returned) && i.UpdatedAt < cutoff;
    }

}
