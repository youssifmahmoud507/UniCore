using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow.Specifications
{
    public sealed class WorkflowDefinitionByIdSpecification : Specification<WorkflowDefinition>
    {
        public WorkflowDefinitionByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(d => d.Id == id);
            AddInclude("Steps");

            if (tracking)
            {
                UseTracking();
            }
        }
    }
}
