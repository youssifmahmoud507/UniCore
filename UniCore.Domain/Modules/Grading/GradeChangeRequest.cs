using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Grading
{
    // An official request to change one score of a Published or Locked grade. The approval is the GRADE_CHANGE workflow
    // (department head, then dean); when it completes the correction is applied through IGradeCorrectionService.
    //   Pending -> Applied | Rejected | Cancelled | Failed
    public sealed class GradeChangeRequest : AggregateRoot
    {
        public const int MaxReasonLength = 500;
        public const int MaxFailureCodeLength = 100;

        private GradeChangeRequest()
        {
        }

        public Guid GradeId { get; private set; }
        public Guid StudentId { get; private set; }
        public Guid CourseSectionId { get; private set; }
        public Guid AssessmentComponentId { get; private set; }
        public decimal OldScore { get; private set; }
        public decimal NewScore { get; private set; }
        public string Reason { get; private set; } = string.Empty;
        public GradeChangeRequestStatus Status { get; private set; }
        public Guid RequestedByUserId { get; private set; }
        public DateTimeOffset RequestedAt { get; private set; }
        public Guid? WorkflowInstanceId { get; private set; }

        // The last approver of the workflow, once the correction was applied.
        public Guid? ApprovedByUserId { get; private set; }
        public DateTimeOffset? ResolvedAt { get; private set; }
        public string? FailureCode { get; private set; }

        public static Result<GradeChangeRequest> Create(Guid gradeId, Guid studentId, Guid courseSectionId, Guid componentId, decimal oldScore, decimal newScore, string? reason, Guid requestedByUserId, DateTimeOffset now)
        {
            var cleanReason = reason?.Trim();

            if (gradeId == Guid.Empty || studentId == Guid.Empty || courseSectionId == Guid.Empty || componentId == Guid.Empty || requestedByUserId == Guid.Empty
                || newScore < 0 || newScore > 1000
                || string.IsNullOrEmpty(cleanReason) || cleanReason.Length > MaxReasonLength)
            {
                return GradeChangeErrors.DataInvalid;
            }

            if (newScore == oldScore)
            {
                return GradeChangeErrors.NoChange;
            }

            return new GradeChangeRequest
            {
                Id = Guid.NewGuid(),
                GradeId = gradeId,
                StudentId = studentId,
                CourseSectionId = courseSectionId,
                AssessmentComponentId = componentId,
                OldScore = oldScore,
                NewScore = newScore,
                Reason = cleanReason,
                Status = GradeChangeRequestStatus.Pending,
                RequestedByUserId = requestedByUserId,
                RequestedAt = now
            };
        }

        public Result AttachWorkflow(Guid workflowInstanceId)
        {
            if (Status != GradeChangeRequestStatus.Pending || WorkflowInstanceId is not null || workflowInstanceId == Guid.Empty)
            {
                return Result.Fail(GradeChangeErrors.InvalidState);
            }

            WorkflowInstanceId = workflowInstanceId;

            return Result.Ok();
        }

        public Result MarkApplied(Guid approvedByUserId, DateTimeOffset now) => Resolve(GradeChangeRequestStatus.Applied, approvedByUserId, null, now);

        public Result MarkRejected(DateTimeOffset now) => Resolve(GradeChangeRequestStatus.Rejected, null, null, now);

        public Result MarkCancelled(DateTimeOffset now) => Resolve(GradeChangeRequestStatus.Cancelled, null, null, now);

        public Result MarkFailed(string failureCode, DateTimeOffset now)
        {
            var code = failureCode.Length > MaxFailureCodeLength ? failureCode[..MaxFailureCodeLength] : failureCode;

            return Resolve(GradeChangeRequestStatus.Failed, null, code, now);
        }

        private Result Resolve(GradeChangeRequestStatus status, Guid? approvedBy, string? failureCode, DateTimeOffset now)
        {
            if (Status != GradeChangeRequestStatus.Pending)
            {
                return Result.Fail(GradeChangeErrors.InvalidState);
            }

            Status = status;
            ApprovedByUserId = approvedBy;
            FailureCode = failureCode;
            ResolvedAt = now;

            return Result.Ok();
        }
    }
}
