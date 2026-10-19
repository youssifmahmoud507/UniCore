using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow
{
    public sealed record WorkflowApprovalDto(Guid Id, Guid StepId, Guid ApproverUserId, WorkflowDecision Decision, string? Comment, DateTimeOffset DecidedAt);



}
