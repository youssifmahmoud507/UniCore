using System.Linq.Expressions;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow
{
    public sealed record CreateWorkflowDefinitionCommand(string? Code, string? Name, IReadOnlyList<WorkflowStepInput>? Steps);


}
