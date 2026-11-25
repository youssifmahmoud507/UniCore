using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.StudentServices
{
    // What every student service request shares: whose it is, where it is in its workflow, and how it ended.
    // The workflow engine (Developer 2) moves the approval; the request only mirrors the outcome, reacting to the engine's events,
    // so every transition here is safe to repeat (the handler treats "already there" as done).
    public abstract class ServiceRequest : AggregateRoot
    {
        public const int TextMaxLength = 2000;
        public const int ShortTextMaxLength = 200;
        public const int NoteMaxLength = 500;

        public Guid StudentId { get; private set; }
        public Guid RequestedByUserId { get; private set; }
        public ServiceRequestStatus Status { get; private set; }
        public Guid? WorkflowInstanceId { get; private set; }
        public DateTimeOffset SubmittedAt { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }
        public DateTimeOffset? DecidedAt { get; private set; }
        public Guid? DecidedByUserId { get; private set; }
        public string? DecisionNote { get; private set; }

        // Open means the workflow can still act on it (or the student can still fix it).
        public bool IsOpen => Status is ServiceRequestStatus.Pending or ServiceRequestStatus.Returned;

        protected Result Initialize(Guid studentId, Guid requestedByUserId, DateTimeOffset now)
        {
            if (studentId == Guid.Empty || requestedByUserId == Guid.Empty)
            {
                return Result.Fail(ServiceRequestErrors.Invalid);
            }

            Id = Guid.NewGuid();
            StudentId = studentId;
            RequestedByUserId = requestedByUserId;
            Status = ServiceRequestStatus.Pending;
            SubmittedAt = now;
            UpdatedAt = now;

            return Result.Ok();
        }

        protected static Result<string> RequiredText(string? value, int maxLength)
        {
            var text = value?.Trim();

            if (string.IsNullOrEmpty(text))
            {
                return ServiceRequestErrors.TextRequired;
            }

            return text.Length > maxLength ? ServiceRequestErrors.TextTooLong : text;
        }

        public Result AttachWorkflow(Guid workflowInstanceId)
        {
            if (workflowInstanceId == Guid.Empty)
            {
                return Result.Fail(ServiceRequestErrors.Invalid);
            }

            if (WorkflowInstanceId is not null)
            {
                return Result.Fail(ServiceRequestErrors.WorkflowAlreadyAttached);
            }

            WorkflowInstanceId = workflowInstanceId;

            return Result.Ok();
        }

        // The last step approved it.
        public Result Approve(Guid? decidedByUserId, DateTimeOffset now)
        {
            if (Status != ServiceRequestStatus.Pending)
            {
                return Result.Fail(ServiceRequestErrors.InvalidState);
            }

            Status = ServiceRequestStatus.Approved;
            Decide(decidedByUserId, null, now);

            return Result.Ok();
        }

        public Result Reject(Guid decidedByUserId, string? note, DateTimeOffset now)
        {
            if (Status != ServiceRequestStatus.Pending)
            {
                return Result.Fail(ServiceRequestErrors.InvalidState);
            }

            var reason = LimitedNote(note);
            if (reason is null)
            {
                return Result.Fail(ServiceRequestErrors.NoteRequired);
            }

            Status = ServiceRequestStatus.Rejected;
            Decide(decidedByUserId, reason, now);

            return Result.Ok();
        }

        // An approver sent it back: the student corrects it and resubmits.
        public Result Return(Guid decidedByUserId, string? note, DateTimeOffset now)
        {
            if (Status != ServiceRequestStatus.Pending)
            {
                return Result.Fail(ServiceRequestErrors.InvalidState);
            }

            var reason = LimitedNote(note);
            if (reason is null)
            {
                return Result.Fail(ServiceRequestErrors.NoteRequired);
            }

            Status = ServiceRequestStatus.Returned;
            Decide(decidedByUserId, reason, now);

            return Result.Ok();
        }

        public Result Resubmit(DateTimeOffset now)
        {
            if (Status != ServiceRequestStatus.Returned)
            {
                return Result.Fail(ServiceRequestErrors.InvalidState);
            }

            Status = ServiceRequestStatus.Pending;
            DecidedAt = null;
            DecidedByUserId = null;
            DecisionNote = null;
            UpdatedAt = now;

            return Result.Ok();
        }

        public Result Cancel(Guid? cancelledByUserId, DateTimeOffset now)
        {
            if (!IsOpen)
            {
                return Result.Fail(ServiceRequestErrors.InvalidState);
            }

            Status = ServiceRequestStatus.Cancelled;
            Decide(cancelledByUserId, null, now);

            return Result.Ok();
        }

        private void Decide(Guid? byUserId, string? note, DateTimeOffset now)
        {
            DecidedAt = now;
            DecidedByUserId = byUserId;
            DecisionNote = note;
            UpdatedAt = now;
        }

        // The engine's comment is trimmed to the column size; an empty one is refused by the caller.
        private static string? LimitedNote(string? note)
        {
            var text = note?.Trim();

            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return text.Length > NoteMaxLength ? text[..NoteMaxLength] : text;
        }
    }
}
