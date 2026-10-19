using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Workflow
{
    // One decision of an approver on one step (approve, reject or return). Only WorkflowInstance creates approvals.
    public sealed class WorkflowApproval : BaseEntity
    {
        private WorkflowApproval()
        {
        }

        public Guid WorkflowInstanceId { get; private set; }
        public Guid WorkflowStepId { get; private set; }
        public Guid ApproverUserId { get; private set; }
        public WorkflowDecision Decision { get; private set; }
        public string? Comment { get; private set; }
        public DateTimeOffset DecidedAt { get; private set; }

        internal static WorkflowApproval Record(Guid instanceId, Guid stepId, Guid approverUserId, WorkflowDecision decision, string? comment, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            WorkflowInstanceId = instanceId,
            WorkflowStepId = stepId,
            ApproverUserId = approverUserId,
            Decision = decision,
            Comment = comment,
            DecidedAt = now
        };
    }

}
