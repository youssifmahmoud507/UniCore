using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow.Specifications
{
    // Every version of a code. Tracked when the handler changes them.
    public sealed class WorkflowDefinitionsByCodeSpecification : Specification<WorkflowDefinition>
    {
        public WorkflowDefinitionsByCodeSpecification(string code, bool tracking = false)
        {
            SetCriteria(d => d.Code == code);

            if (tracking)
            {
                UseTracking();
            }
        }
    }
}
