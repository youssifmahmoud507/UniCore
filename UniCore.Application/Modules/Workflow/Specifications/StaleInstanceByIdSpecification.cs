using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow.Specifications
{
    public sealed class StaleInstanceByIdSpecification : Specification<WorkflowInstance>
    {
        public StaleInstanceByIdSpecification(Guid id, DateTimeOffset cutoff)
        {
            SetCriteria(i => i.Id == id && (i.Status == WorkflowInstanceStatus.InProgress || i.Status == WorkflowInstanceStatus.Returned) && i.UpdatedAt < cutoff);
            UseTracking();
        }
    }

}
