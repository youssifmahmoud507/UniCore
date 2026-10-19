using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow.Specifications
{
    public sealed class WorkflowInstanceByIdSpecification : Specification<WorkflowInstance>
    {
        public WorkflowInstanceByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(i => i.Id == id);
            AddInclude("Actions");
            AddInclude("Approvals");

            if (tracking)
            {
                UseTracking();
            }
        }
    }
}
