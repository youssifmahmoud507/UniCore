using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow.Specifications
{
    public sealed class WorkflowDefinitionsListSpecification : Specification<WorkflowDefinition>
    {
        public WorkflowDefinitionsListSpecification(PaginationQuery query, string? code, bool? isActive)
        {
            SetCriteria(d => (code == null || d.Code == code) && (isActive == null || d.IsActive == isActive));
            AddInclude("Steps");
            ApplyOrderBy(d => d.Code);
            ApplyPaging(query);
        }
    }
}
