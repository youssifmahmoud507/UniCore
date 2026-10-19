using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Workflow
{
    // Append-only log of everything that happened to an instance. Only WorkflowInstance creates actions.
    public sealed class WorkflowAction : BaseEntity
    {
        public const int MaxCommentLength = 500;

        private WorkflowAction()
        {
        }

        public Guid WorkflowInstanceId { get; private set; }
        public Guid? WorkflowStepId { get; private set; }
        public WorkflowActionType ActionType { get; private set; }
        public Guid PerformedByUserId { get; private set; }
        public string? Comment { get; private set; }
        public DateTimeOffset PerformedAt { get; private set; }

        internal static WorkflowAction Record(Guid instanceId, Guid? stepId, WorkflowActionType type, Guid performedBy, string? comment, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            WorkflowInstanceId = instanceId,
            WorkflowStepId = stepId,
            ActionType = type,
            PerformedByUserId = performedBy,
            Comment = comment,
            PerformedAt = now
        };
    }

}
