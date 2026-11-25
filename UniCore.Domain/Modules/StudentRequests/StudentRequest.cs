using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.StudentRequests
{
    // What a withdrawal request and a leave request share: a student asks, a workflow approves, and when it completes the
    // request is applied (or it fails and says why). Not a table of its own: each request type has its table.
    public abstract class StudentRequest : AggregateRoot
    {
        public const int MaxReasonLength = 500;
        public const int MaxFailureCodeLength = 100;

        public Guid StudentId { get; protected set; }
        public string Reason { get; protected set; } = string.Empty;
        public AcademicRequestStatus Status { get; protected set; }
        public Guid RequestedByUserId { get; protected set; }
        public DateTimeOffset RequestedAt { get; protected set; }
        public Guid? WorkflowInstanceId { get; protected set; }

        // The last approver of the workflow, once the request was applied.
        public Guid? ApprovedByUserId { get; protected set; }
        public DateTimeOffset? ResolvedAt { get; protected set; }
        public string? FailureCode { get; protected set; }

        protected static string? CleanReason(string? reason)
        {
            var clean = reason?.Trim();

            return string.IsNullOrEmpty(clean) || clean.Length > MaxReasonLength ? null : clean;
        }

        public Result AttachWorkflow(Guid workflowInstanceId)
        {
            if (Status != AcademicRequestStatus.Pending || WorkflowInstanceId is not null || workflowInstanceId == Guid.Empty)
            {
                return Result.Fail(StudentRequestErrors.InvalidState);
            }

            WorkflowInstanceId = workflowInstanceId;

            return Result.Ok();
        }

        public Result MarkApplied(Guid approvedByUserId, DateTimeOffset now) => Resolve(AcademicRequestStatus.Applied, approvedByUserId, null, now);

        public Result MarkRejected(DateTimeOffset now) => Resolve(AcademicRequestStatus.Rejected, null, null, now);

        public Result MarkCancelled(DateTimeOffset now) => Resolve(AcademicRequestStatus.Cancelled, null, null, now);

        public Result MarkFailed(string failureCode, DateTimeOffset now)
        {
            var code = failureCode.Length > MaxFailureCodeLength ? failureCode[..MaxFailureCodeLength] : failureCode;

            return Resolve(AcademicRequestStatus.Failed, null, code, now);
        }

        private Result Resolve(AcademicRequestStatus status, Guid? approvedBy, string? failureCode, DateTimeOffset now)
        {
            if (Status != AcademicRequestStatus.Pending)
            {
                return Result.Fail(StudentRequestErrors.InvalidState);
            }

            Status = status;
            ApprovedByUserId = approvedBy;
            FailureCode = failureCode;
            ResolvedAt = now;

            return Result.Ok();
        }
    }
}
