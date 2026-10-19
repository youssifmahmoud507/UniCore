using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Domain.Modules.Workflow
{
    // One run of a definition for one request. Pinned to the definition version it started with.
    // Role and scope checks (who may act) are made by the Application layer; this class keeps the state machine:
    //   InProgress -> Completed | Rejected | Cancelled | Returned,   Returned -> InProgress (resubmit) | Cancelled.
    public sealed class WorkflowInstance : AggregateRoot
    {
        public const int MaxReferenceTypeLength = 50;

        private readonly List<WorkflowAction> _actions = [];
        private readonly List<WorkflowApproval> _approvals = [];

        private WorkflowInstance()
        {
        }

        public Guid WorkflowDefinitionId { get; private set; }
        public string DefinitionCode { get; private set; } = string.Empty;
        public int DefinitionVersion { get; private set; }
        public Guid RequesterUserId { get; private set; }
        public Guid? SubjectStudentId { get; private set; }

        // Where the request sits (department or faculty of the student). Approvers must have this scope.
        public ScopeType? ScopeType { get; private set; }
        public Guid? ScopeId { get; private set; }

        // The request this instance belongs to (for example "WithdrawalRequest" and its id).
        public string ReferenceType { get; private set; } = string.Empty;
        public Guid ReferenceId { get; private set; }

        public WorkflowInstanceStatus Status { get; private set; }
        public Guid? CurrentStepId { get; private set; }
        public int CurrentStepOrder { get; private set; }
        public string? CurrentStepName { get; private set; }

        // The role that must act now. Null once the instance is finished or returned. Lets the inbox filter without a join.
        public string? CurrentRole { get; private set; }
        public int TotalSteps { get; private set; }
        public DateTimeOffset StartedAt { get; private set; }
        public DateTimeOffset? CompletedAt { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }

        public IReadOnlyList<WorkflowAction> Actions => _actions.AsReadOnly();
        public IReadOnlyList<WorkflowApproval> Approvals => _approvals.AsReadOnly();

        public bool IsFinished => Status is WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Rejected or WorkflowInstanceStatus.Cancelled;

        public static Result<WorkflowInstance> Start(WorkflowDefinition definition, Guid requesterUserId, string? referenceType, Guid referenceId, Guid? subjectStudentId, ScopeType? scopeType, Guid? scopeId, DateTimeOffset now)
        {
            if (!definition.IsActive)
            {
                return WorkflowErrors.DefinitionInactive;
            }

            var cleanType = referenceType?.Trim();

            // A scope is either complete (a type below Global and an id) or absent.
            var scopeOk = (scopeType is null && scopeId is null)
                || (scopeType is { } type && type != Identity.ScopeType.Global && scopeId is { } id && id != Guid.Empty);

            if (requesterUserId == Guid.Empty
                || referenceId == Guid.Empty
                || string.IsNullOrEmpty(cleanType)
                || cleanType.Length > MaxReferenceTypeLength
                || (subjectStudentId is { } student && student == Guid.Empty)
                || !scopeOk)
            {
                return WorkflowErrors.RequestDataInvalid;
            }

            var first = definition.FirstStep;

            var instance = new WorkflowInstance
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = definition.Id,
                DefinitionCode = definition.Code,
                DefinitionVersion = definition.Version,
                RequesterUserId = requesterUserId,
                SubjectStudentId = subjectStudentId,
                ScopeType = scopeType,
                ScopeId = scopeId,
                ReferenceType = cleanType,
                ReferenceId = referenceId,
                Status = WorkflowInstanceStatus.InProgress,
                TotalSteps = definition.Steps.Count,
                StartedAt = now,
                UpdatedAt = now
            };

            instance.MoveTo(first);
            instance._actions.Add(WorkflowAction.Record(instance.Id, first.Id, WorkflowActionType.Started, requesterUserId, null, now));
            instance.RaiseDomainEvent(new WorkflowStarted(Guid.NewGuid(), now, instance.Id, instance.DefinitionCode, instance.ReferenceType, instance.ReferenceId, requesterUserId, subjectStudentId, first.RequiredRole));

            return instance;
        }

        // The approver of the current step says yes: the next step starts, or the instance is completed.
        public Result Approve(WorkflowDefinition definition, Guid approverUserId, string? comment, DateTimeOffset now)
        {
            var step = CheckCanDecide(definition, approverUserId, comment, out var cleanComment);
            if (!step.TryGetValue(out var current))
            {
                return Result.Fail(step.Errors);
            }

            _approvals.Add(WorkflowApproval.Record(Id, current.Id, approverUserId, WorkflowDecision.Approved, cleanComment, now));
            _actions.Add(WorkflowAction.Record(Id, current.Id, WorkflowActionType.Approved, approverUserId, cleanComment, now));
            UpdatedAt = now;

            var next = definition.NextAfter(current.Order);

            if (next is null)
            {
                Status = WorkflowInstanceStatus.Completed;
                CompletedAt = now;
                CurrentRole = null;
                RaiseDomainEvent(new WorkflowCompleted(Guid.NewGuid(), now, Id, DefinitionCode, ReferenceType, ReferenceId, RequesterUserId, SubjectStudentId));

                return Result.Ok();
            }

            MoveTo(next);
            RaiseDomainEvent(new WorkflowStepAdvanced(Guid.NewGuid(), now, Id, DefinitionCode, ReferenceType, ReferenceId, RequesterUserId, next.Order, next.Name, next.RequiredRole));

            return Result.Ok();
        }

        // The approver says no. The comment is mandatory. The instance ends.
        public Result Reject(WorkflowDefinition definition, Guid approverUserId, string? comment, DateTimeOffset now)
        {
            var step = CheckCanDecide(definition, approverUserId, comment, out var cleanComment);
            if (!step.TryGetValue(out var current))
            {
                return Result.Fail(step.Errors);
            }

            if (cleanComment is null)
            {
                return Result.Fail(WorkflowErrors.CommentRequired);
            }

            _approvals.Add(WorkflowApproval.Record(Id, current.Id, approverUserId, WorkflowDecision.Rejected, cleanComment, now));
            _actions.Add(WorkflowAction.Record(Id, current.Id, WorkflowActionType.Rejected, approverUserId, cleanComment, now));

            Status = WorkflowInstanceStatus.Rejected;
            CompletedAt = now;
            UpdatedAt = now;
            CurrentRole = null;

            RaiseDomainEvent(new WorkflowRejected(Guid.NewGuid(), now, Id, DefinitionCode, ReferenceType, ReferenceId, RequesterUserId, SubjectStudentId, approverUserId, cleanComment));

            return Result.Ok();
        }

        // The approver sends it back for correction. The comment is mandatory. The requester can resubmit.
        public Result Return(WorkflowDefinition definition, Guid approverUserId, string? comment, DateTimeOffset now)
        {
            var step = CheckCanDecide(definition, approverUserId, comment, out var cleanComment);
            if (!step.TryGetValue(out var current))
            {
                return Result.Fail(step.Errors);
            }

            if (cleanComment is null)
            {
                return Result.Fail(WorkflowErrors.CommentRequired);
            }

            _approvals.Add(WorkflowApproval.Record(Id, current.Id, approverUserId, WorkflowDecision.Returned, cleanComment, now));
            _actions.Add(WorkflowAction.Record(Id, current.Id, WorkflowActionType.Returned, approverUserId, cleanComment, now));

            Status = WorkflowInstanceStatus.Returned;
            UpdatedAt = now;
            CurrentRole = null;

            RaiseDomainEvent(new WorkflowReturned(Guid.NewGuid(), now, Id, DefinitionCode, ReferenceType, ReferenceId, RequesterUserId, SubjectStudentId, approverUserId, cleanComment));

            return Result.Ok();
        }

        // The requester fixed what was asked and sends it again: it restarts at step 1.
        public Result Resubmit(WorkflowDefinition definition, Guid requesterUserId, string? comment, DateTimeOffset now)
        {
            if (Status != WorkflowInstanceStatus.Returned || definition.Id != WorkflowDefinitionId)
            {
                return Result.Fail(WorkflowErrors.InvalidState);
            }

            if (requesterUserId != RequesterUserId)
            {
                return Result.Fail(WorkflowErrors.NotRequester);
            }

            var clean = CleanComment(comment);
            if (clean.Error is { } bad)
            {
                return Result.Fail(bad);
            }

            var first = definition.FirstStep;

            Status = WorkflowInstanceStatus.InProgress;
            UpdatedAt = now;
            MoveTo(first);

            _actions.Add(WorkflowAction.Record(Id, first.Id, WorkflowActionType.Resubmitted, requesterUserId, clean.Value, now));
            RaiseDomainEvent(new WorkflowStepAdvanced(Guid.NewGuid(), now, Id, DefinitionCode, ReferenceType, ReferenceId, RequesterUserId, first.Order, first.Name, first.RequiredRole));

            return Result.Ok();
        }

        // The requester, or an admin, stops it while it is not finished.
        public Result Cancel(Guid cancelledByUserId, bool byAdmin, string? comment, DateTimeOffset now)
        {
            if (IsFinished)
            {
                return Result.Fail(WorkflowErrors.InvalidState);
            }

            if (cancelledByUserId != RequesterUserId && !byAdmin)
            {
                return Result.Fail(WorkflowErrors.NotRequester);
            }

            var clean = CleanComment(comment);
            if (clean.Error is { } bad)
            {
                return Result.Fail(bad);
            }

            _actions.Add(WorkflowAction.Record(Id, CurrentStepId, WorkflowActionType.Cancelled, cancelledByUserId, clean.Value, now));

            Status = WorkflowInstanceStatus.Cancelled;
            CompletedAt = now;
            UpdatedAt = now;
            CurrentRole = null;

            RaiseDomainEvent(new WorkflowCancelled(Guid.NewGuid(), now, Id, DefinitionCode, ReferenceType, ReferenceId, RequesterUserId, SubjectStudentId, cancelledByUserId));

            return Result.Ok();
        }

        private void MoveTo(WorkflowStep step)
        {
            CurrentStepId = step.Id;
            CurrentStepOrder = step.Order;
            CurrentStepName = step.Name;
            CurrentRole = step.RequiredRole;
        }

        // The checks every decision shares: the instance is in progress, the definition is its own,
        // the approver is not the requester, the comment is short enough, and the current step still exists.
        private Result<WorkflowStep> CheckCanDecide(WorkflowDefinition definition, Guid approverUserId, string? comment, out string? cleanComment)
        {
            cleanComment = null;

            if (Status != WorkflowInstanceStatus.InProgress || definition.Id != WorkflowDefinitionId || CurrentStepId is not { } stepId)
            {
                return WorkflowErrors.InvalidState;
            }

            if (approverUserId == Guid.Empty)
            {
                return WorkflowErrors.RequestDataInvalid;
            }

            if (approverUserId == RequesterUserId)
            {
                return WorkflowErrors.SelfApproval;
            }

            var clean = CleanComment(comment);
            if (clean.Error is { } bad)
            {
                return bad;
            }

            cleanComment = clean.Value;

            var step = definition.StepById(stepId);

            return step is null ? WorkflowErrors.InvalidState : step;
        }

        private static (string? Value, Error? Error) CleanComment(string? comment)
        {
            var clean = comment?.Trim();

            if (string.IsNullOrEmpty(clean))
            {
                return (null, null);
            }

            return clean.Length > WorkflowAction.MaxCommentLength ? (null, WorkflowErrors.RequestDataInvalid) : (clean, null);
        }
    }

}
