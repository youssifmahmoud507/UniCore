using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Grading
{
    // What the grading scale made of the scores: percentage, letter, points, pass or fail.
    public sealed record GradeResult(Guid GradeScaleId, decimal Percentage, string Letter, decimal GradePoints, bool IsPassing);

    // The grade of ONE enrollment (one student in one section). Aggregate root of its component scores.
    // Lifecycle: Draft -> Submitted -> Reviewed -> Approved -> Published -> Locked; Submitted/Reviewed -> Draft with a reason.
    // Course, semester and credits are snapshots taken when the grade is created, so GPA never reads another module's tables.
    // The person who entered a grade cannot review or approve it, and the reviewer cannot approve it.
    public sealed class Grade : AggregateRoot
    {
        public const int ReasonMaxLength = 500;
        public const decimal MaxScore = 1000m;

        private readonly List<GradeComponentScore> _scores = [];

        private Grade()
        {
        }

        public Guid EnrollmentId { get; private set; }
        public Guid StudentId { get; private set; }
        public Guid CourseSectionId { get; private set; }
        public Guid AssessmentId { get; private set; }
        public Guid AcademicSemesterId { get; private set; }
        public Guid CourseId { get; private set; }
        public int Credits { get; private set; }

        public GradeStatus Status { get; private set; }

        public Guid? GradeScaleId { get; private set; }
        public decimal? Percentage { get; private set; }
        public string? Letter { get; private set; }
        public decimal? GradePoints { get; private set; }
        public bool? IsPassing { get; private set; }

        public Guid? EnteredByUserId { get; private set; }
        public Guid? ReviewedByUserId { get; private set; }
        public Guid? ApprovedByUserId { get; private set; }
        public DateTimeOffset? SubmittedAt { get; private set; }
        public DateTimeOffset? ReviewedAt { get; private set; }
        public DateTimeOffset? ApprovedAt { get; private set; }
        public DateTimeOffset? PublishedAt { get; private set; }
        public DateTimeOffset? LockedAt { get; private set; }
        public string? ReturnReason { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public IReadOnlyList<GradeComponentScore> Scores => _scores.AsReadOnly();

        public bool IsFinal => Status is GradeStatus.Published or GradeStatus.Locked;

        public static Result<Grade> Create(
            Guid enrollmentId, Guid studentId, Guid courseSectionId, Guid assessmentId, Guid academicSemesterId, Guid courseId, int credits, DateTimeOffset now)
        {
            if (enrollmentId == Guid.Empty || studentId == Guid.Empty || courseSectionId == Guid.Empty || assessmentId == Guid.Empty
                || academicSemesterId == Guid.Empty || courseId == Guid.Empty || credits < 0)
            {
                return GradingErrors.Invalid;
            }

            return new Grade
            {
                Id = Guid.NewGuid(), EnrollmentId = enrollmentId, StudentId = studentId, CourseSectionId = courseSectionId, AssessmentId = assessmentId,
                AcademicSemesterId = academicSemesterId, CourseId = courseId, Credits = credits, Status = GradeStatus.Draft, CreatedAt = now
            };
        }

        // Draft only. A new score for a component replaces the old one. The maximum of the component is checked by the handler.
        public Result SetScore(Guid componentId, decimal score, Guid? enteredByUserId, DateTimeOffset now)
        {
            if (IsFinal)
            {
                return Result.Fail(GradingErrors.Locked);
            }

            if (Status != GradeStatus.Draft)
            {
                return Result.Fail(GradingErrors.InvalidState);
            }

            if (componentId == Guid.Empty || !IsValidScore(score))
            {
                return Result.Fail(GradingErrors.ScoreInvalid);
            }

            var existing = _scores.FirstOrDefault(s => s.AssessmentComponentId == componentId);
            if (existing is null)
            {
                _scores.Add(GradeComponentScore.Create(Id, componentId, score));
            }
            else
            {
                existing.Change(score);
            }

            EnteredByUserId = enteredByUserId;
            UpdatedAt = now;
            return Result.Ok();
        }

        // Draft -> Submitted. Every component of the scheme needs a score; `result` is what the scale made of them.
        public Result Submit(IReadOnlyCollection<Guid> componentIds, GradeResult result, Guid? submittedByUserId, DateTimeOffset now)
        {
            if (IsFinal)
            {
                return Result.Fail(GradingErrors.Locked);
            }

            if (Status != GradeStatus.Draft)
            {
                return Result.Fail(GradingErrors.InvalidState);
            }

            if (componentIds.Count == 0 || componentIds.Any(id => _scores.All(s => s.AssessmentComponentId != id)))
            {
                return Result.Fail(GradingErrors.ScoresIncomplete);
            }

            ApplyResult(result);
            EnteredByUserId = submittedByUserId ?? EnteredByUserId;
            Status = GradeStatus.Submitted;
            SubmittedAt = now;
            UpdatedAt = now;
            ReturnReason = null;
            return Result.Ok();
        }

        // Submitted or Reviewed -> Draft. The reviews so far are forgotten; the instructor corrects and submits again.
        public Result ReturnToDraft(string? reason, DateTimeOffset now)
        {
            if (IsFinal)
            {
                return Result.Fail(GradingErrors.Locked);
            }

            if (Status is not (GradeStatus.Submitted or GradeStatus.Reviewed))
            {
                return Result.Fail(GradingErrors.InvalidState);
            }

            var clean = reason?.Trim();
            if (string.IsNullOrWhiteSpace(clean) || clean.Length > ReasonMaxLength)
            {
                return Result.Fail(GradingErrors.ReasonRequired);
            }

            Status = GradeStatus.Draft;
            ReturnReason = clean;
            ReviewedByUserId = null;
            ReviewedAt = null;
            SubmittedAt = null;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Review(Guid reviewerUserId, DateTimeOffset now)
        {
            if (Status != GradeStatus.Submitted)
            {
                return Result.Fail(IsFinal ? GradingErrors.Locked : GradingErrors.InvalidState);
            }

            if (reviewerUserId == Guid.Empty || reviewerUserId == EnteredByUserId)
            {
                return Result.Fail(GradingErrors.SeparationOfDuties);
            }

            Status = GradeStatus.Reviewed;
            ReviewedByUserId = reviewerUserId;
            ReviewedAt = now;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Approve(Guid approverUserId, DateTimeOffset now)
        {
            if (Status != GradeStatus.Reviewed)
            {
                return Result.Fail(IsFinal ? GradingErrors.Locked : GradingErrors.InvalidState);
            }

            if (approverUserId == Guid.Empty || approverUserId == EnteredByUserId || approverUserId == ReviewedByUserId)
            {
                return Result.Fail(GradingErrors.SeparationOfDuties);
            }

            Status = GradeStatus.Approved;
            ApprovedByUserId = approverUserId;
            ApprovedAt = now;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Publish(DateTimeOffset now)
        {
            if (Status != GradeStatus.Approved)
            {
                return Result.Fail(IsFinal ? GradingErrors.Locked : GradingErrors.InvalidState);
            }

            Status = GradeStatus.Published;
            PublishedAt = now;
            UpdatedAt = now;

            RaiseDomainEvent(new GradePublished(
                Guid.NewGuid(), now, Id, EnrollmentId, StudentId, CourseSectionId, AcademicSemesterId, CourseId, Credits,
                Percentage!.Value, Letter!, GradePoints!.Value, IsPassing!.Value));
            return Result.Ok();
        }

        public Result Lock(DateTimeOffset now)
        {
            if (Status != GradeStatus.Published)
            {
                return Result.Fail(Status == GradeStatus.Locked ? GradingErrors.Locked : GradingErrors.InvalidState);
            }

            Status = GradeStatus.Locked;
            LockedAt = now;
            UpdatedAt = now;
            return Result.Ok();
        }

        // The ONLY way a Published or Locked grade changes: an approved grade change. The status stays; GPA and the student are told.
        public Result Correct(Guid componentId, decimal newScore, GradeResult result, string? reason, DateTimeOffset now)
        {
            var clean = reason?.Trim();
            var existing = _scores.FirstOrDefault(s => s.AssessmentComponentId == componentId);

            if (!IsFinal)
            {
                return Result.Fail(GradingErrors.InvalidState);
            }

            if (existing is null || !IsValidScore(newScore) || string.IsNullOrWhiteSpace(clean) || clean.Length > ReasonMaxLength)
            {
                return Result.Fail(GradingErrors.CorrectionInvalid);
            }

            var oldPercentage = Percentage!.Value;
            var oldLetter = Letter!;

            existing.Change(newScore);
            ApplyResult(result);
            UpdatedAt = now;

            RaiseDomainEvent(new GradeCorrected(
                Guid.NewGuid(), now, Id, EnrollmentId, StudentId, CourseSectionId, AcademicSemesterId, CourseId, Credits,
                oldPercentage, oldLetter, result.Percentage, result.Letter, result.GradePoints, result.IsPassing));
            return Result.Ok();
        }

        private void ApplyResult(GradeResult result)
        {
            GradeScaleId = result.GradeScaleId;
            Percentage = result.Percentage;
            Letter = result.Letter;
            GradePoints = result.GradePoints;
            IsPassing = result.IsPassing;
        }

        private static bool IsValidScore(decimal score) => score >= 0 && score <= MaxScore && decimal.Round(score, 2) == score;
    }
}
