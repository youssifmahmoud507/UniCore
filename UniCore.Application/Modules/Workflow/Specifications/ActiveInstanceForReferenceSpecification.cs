using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow.Specifications
{
    // A request has at most one live workflow (in progress, or returned and waiting for its requester).
    public sealed class ActiveInstanceForReferenceSpecification : Specification<WorkflowInstance>
    {
        public ActiveInstanceForReferenceSpecification(string referenceType, Guid referenceId)
        {
            SetCriteria(i => i.ReferenceType == referenceType && i.ReferenceId == referenceId && (i.Status == WorkflowInstanceStatus.InProgress || i.Status == WorkflowInstanceStatus.Returned));
        }
    }
}
