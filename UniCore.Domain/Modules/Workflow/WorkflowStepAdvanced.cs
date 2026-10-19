using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Workflow
{
    // The instance moved to a step (after an approval, or after a resubmit). Tells the approvers of that step.
    public sealed record WorkflowStepAdvanced(Guid EventId, DateTimeOffset OccurredAt, Guid InstanceId, string DefinitionCode, string ReferenceType, Guid ReferenceId, Guid RequesterUserId, int StepOrder, string StepName, string RequiredRole) : IDomainEvent;

}
