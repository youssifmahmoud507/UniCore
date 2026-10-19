namespace UniCore.Api.Contracts.Workflow
{
    public sealed record CreateWorkflowDefinitionRequest(string? Code, string? Name, List<WorkflowStepRequest>? Steps);

}
