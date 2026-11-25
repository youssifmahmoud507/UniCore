using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Students;

namespace UniCore.Domain.Modules.Graduation
{
    // A student's request to graduate from the program and study plan version they follow. Only one Submitted or Approved
    // application can exist per student (a filtered unique index in the database closes the race between two requests).
    public sealed class GraduationApplication : AggregateRoot
    {
        public const int NoteMaxLength = 500;

        private GraduationApplication()
        {
        }

        public Guid StudentId { get; private set; }
        public Guid AcademicProgramId { get; private set; }
        public Guid StudyPlanVersionId { get; private set; }
        public GraduationApplicationStatus Status { get; private set; }
        public DateTimeOffset SubmittedAt { get; private set; }

        // The figures at the time of the decision.
        public decimal? CumulativeGpa { get; private set; }
        public int? EarnedCredits { get; private set; }
        public AcademicStanding? Standing { get; private set; }

        public Guid? DecidedByUserId { get; private set; }
        public DateTimeOffset? DecidedAt { get; private set; }
        public string? DecisionNote { get; private set; }
        public DateTimeOffset? CancelledAt { get; private set; }

        public bool IsActive => Status == GraduationApplicationStatus.Submitted;

        public static Result<GraduationApplication> Submit(Guid studentId, Guid academicProgramId, Guid studyPlanVersionId, DateTimeOffset now)
        {
            if (studentId == Guid.Empty || academicProgramId == Guid.Empty || studyPlanVersionId == Guid.Empty)
            {
                return GraduationErrors.Invalid;
            }

            return new GraduationApplication
            {
                Id = Guid.NewGuid(), StudentId = studentId, AcademicProgramId = academicProgramId, StudyPlanVersionId = studyPlanVersionId,
                Status = GraduationApplicationStatus.Submitted, SubmittedAt = now
            };
        }

        public Result Cancel(DateTimeOffset now)
        {
            if (Status != GraduationApplicationStatus.Submitted)
            {
                return Result.Fail(GraduationErrors.InvalidState);
            }

            Status = GraduationApplicationStatus.Cancelled;
            CancelledAt = now;

            return Result.Ok();
        }

        public Result Approve(Guid decidedByUserId, decimal cumulativeGpa, int earnedCredits, AcademicStanding? standing, DateOnly graduationDate, string? note, DateTimeOffset now)
        {
            if (decidedByUserId == Guid.Empty || cumulativeGpa < 0 || earnedCredits < 0 || Clean(note) is { Length: > NoteMaxLength })
            {
                return Result.Fail(GraduationErrors.Invalid);
            }

            if (Status != GraduationApplicationStatus.Submitted)
            {
                return Result.Fail(GraduationErrors.InvalidState);
            }

            Status = GraduationApplicationStatus.Approved;
            CumulativeGpa = cumulativeGpa;
            EarnedCredits = earnedCredits;
            Standing = standing;
            DecidedByUserId = decidedByUserId;
            DecidedAt = now;
            DecisionNote = Clean(note);

            RaiseDomainEvent(new GraduationApproved(
                Guid.NewGuid(), now, Id, StudentId, AcademicProgramId, StudyPlanVersionId, cumulativeGpa, earnedCredits, standing, graduationDate, decidedByUserId));

            return Result.Ok();
        }

        public Result Reject(Guid decidedByUserId, string? reason, DateTimeOffset now)
        {
            var clean = Clean(reason);

            if (decidedByUserId == Guid.Empty)
            {
                return Result.Fail(GraduationErrors.Invalid);
            }

            if (clean is null)
            {
                return Result.Fail(GraduationErrors.ReasonRequired);
            }

            if (clean.Length > NoteMaxLength)
            {
                return Result.Fail(GraduationErrors.Invalid);
            }

            if (Status != GraduationApplicationStatus.Submitted)
            {
                return Result.Fail(GraduationErrors.InvalidState);
            }

            Status = GraduationApplicationStatus.Rejected;
            DecidedByUserId = decidedByUserId;
            DecidedAt = now;
            DecisionNote = clean;

            return Result.Ok();
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
