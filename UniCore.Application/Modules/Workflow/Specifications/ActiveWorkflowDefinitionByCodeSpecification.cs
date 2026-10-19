using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow.Specifications
{
    // The one active version of a code (a database index allows only one).
    public sealed class ActiveWorkflowDefinitionByCodeSpecification : Specification<WorkflowDefinition>
    {
        public ActiveWorkflowDefinitionByCodeSpecification(string code)
        {
            SetCriteria(d => d.Code == code && d.IsActive);
            AddInclude("Steps");
        }
    }
}
