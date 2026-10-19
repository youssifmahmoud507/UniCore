using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow
{
    public sealed record WorkflowActionDto(Guid Id, Guid? StepId, WorkflowActionType ActionType, Guid PerformedByUserId, string? Comment, DateTimeOffset PerformedAt);



}
