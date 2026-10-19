using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow
{
    public sealed record WorkflowInstanceDto(Guid Id, Guid WorkflowDefinitionId, string DefinitionCode, int DefinitionVersion, Guid RequesterUserId, Guid? SubjectStudentId, ScopeType? ScopeType, Guid? ScopeId, string ReferenceType, Guid ReferenceId, WorkflowInstanceStatus Status, int CurrentStepOrder, string? CurrentStepName, string? CurrentRole, int TotalSteps, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, IReadOnlyList<WorkflowActionDto> Actions, IReadOnlyList<WorkflowApprovalDto> Approvals);



}
