namespace UniCore.Application.Modules.Workflow
{
    public sealed record WorkflowDefinitionDto(Guid Id, string Code, string Name, int Version, bool IsActive, IReadOnlyList<WorkflowStepDto> Steps, DateTimeOffset CreatedAt);



}
