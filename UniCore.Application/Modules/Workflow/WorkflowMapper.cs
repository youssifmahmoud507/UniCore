using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow
{
    public static class WorkflowMapper
    {
        public static WorkflowDefinitionDto ToDto(WorkflowDefinition d) => new(d.Id, d.Code, d.Name, d.Version, d.IsActive, [.. d.Steps.OrderBy(s => s.Order).Select(s => new WorkflowStepDto(s.Id, s.Order, s.Name, s.RequiredRole))], d.CreatedAt);
        public static WorkflowInstanceDto ToDto(WorkflowInstance i) => new(i.Id, i.WorkflowDefinitionId, i.DefinitionCode, i.DefinitionVersion, i.RequesterUserId, i.SubjectStudentId, i.ScopeType, i.ScopeId, i.ReferenceType, i.ReferenceId, i.Status, i.CurrentStepOrder, i.CurrentStepName, i.CurrentRole, i.TotalSteps, i.StartedAt, i.CompletedAt, [.. i.Actions.OrderBy(a => a.PerformedAt).Select(a => new WorkflowActionDto(a.Id, a.WorkflowStepId, a.ActionType, a.PerformedByUserId, a.Comment, a.PerformedAt))], [.. i.Approvals.OrderBy(a => a.DecidedAt).Select(a => new WorkflowApprovalDto(a.Id, a.WorkflowStepId, a.ApproverUserId, a.Decision, a.Comment, a.DecidedAt))]);
        public static WorkflowInstanceSummaryDto ToSummary(WorkflowInstance i) => new(i.Id, i.DefinitionCode, i.RequesterUserId, i.SubjectStudentId, i.ReferenceType, i.ReferenceId, i.Status, i.CurrentStepOrder, i.CurrentStepName, i.CurrentRole, i.TotalSteps, i.StartedAt, i.CompletedAt);
    }



}
