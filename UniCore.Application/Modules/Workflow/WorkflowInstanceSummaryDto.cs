using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow
{
    public sealed record WorkflowInstanceSummaryDto(Guid Id, string DefinitionCode, Guid RequesterUserId, Guid? SubjectStudentId, string ReferenceType, Guid ReferenceId, WorkflowInstanceStatus Status, int CurrentStepOrder, string? CurrentStepName, string? CurrentRole, int TotalSteps, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt);



}
