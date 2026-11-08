using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Attendance
{
    // A student's excuse for one absence. Only the id of the supporting document is kept (the file lives in Documents).
    // Pending -> Accepted (the absence becomes Excused) or Rejected (a note is required).
    public sealed class AttendanceExcuse : AggregateRoot
    {
        private AttendanceExcuse()
        {
        }

        public Guid AttendanceSessionId { get; private set; }
        public Guid AttendanceRecordId { get; private set; }
        public Guid StudentId { get; private set; }
        public Guid CourseSectionId { get; private set; }
        public string Reason { get; private set; } = string.Empty;
        public Guid? DocumentId { get; private set; }
        public ExcuseStatus Status { get; private set; }
        public DateTimeOffset SubmittedAt { get; private set; }
        public Guid? ReviewedByUserId { get; private set; }
        public DateTimeOffset? ReviewedAt { get; private set; }
        public string? ReviewNote { get; private set; }

        public static Result<AttendanceExcuse> Submit(Guid sessionId, Guid recordId, Guid studentId, Guid courseSectionId, string? reason, Guid? documentId, DateTimeOffset now)
        {
            var clean = reason?.Trim();
            if (sessionId == Guid.Empty || recordId == Guid.Empty || studentId == Guid.Empty || courseSectionId == Guid.Empty || clean is null || clean.Length is < 5 or > 1000 || documentId == Guid.Empty)
            {
                return AttendanceErrors.ExcuseInvalid;
            }

            return new AttendanceExcuse
            {
                Id = Guid.NewGuid(),
                AttendanceSessionId = sessionId,
                AttendanceRecordId = recordId,
                StudentId = studentId,
                CourseSectionId = courseSectionId,
                Reason = clean,
                DocumentId = documentId,
                Status = ExcuseStatus.Pending,
                SubmittedAt = now
            };
        }

        public Result Accept(Guid reviewerUserId, string? note, DateTimeOffset now)
            => Review(ExcuseStatus.Accepted, reviewerUserId, note, now);

        public Result Reject(Guid reviewerUserId, string? note, DateTimeOffset now)
            => Review(ExcuseStatus.Rejected, reviewerUserId, note, now);

        private Result Review(ExcuseStatus outcome, Guid reviewerUserId, string? note, DateTimeOffset now)
        {
            if (Status != ExcuseStatus.Pending)
            {
                return Result.Fail(AttendanceErrors.ExcuseNotPending);
            }

            var clean = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (reviewerUserId == Guid.Empty || (clean is not null && clean.Length > 1000) || (outcome == ExcuseStatus.Rejected && clean is null))
            {
                return Result.Fail(AttendanceErrors.ExcuseRejectNeedsNote);
            }

            Status = outcome;
            ReviewedByUserId = reviewerUserId;
            ReviewedAt = now;
            ReviewNote = clean;
            return Result.Ok();
        }
    }
}
