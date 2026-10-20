using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Admissions
{
    // An applicant's application to one program for one academic year. It owns its requirements and its
    // decisions; every change goes through the methods below, so the state machine cannot be bypassed.
    // Rules: academic-lifecycle.md section 6.
    public sealed class AdmissionApplication : AggregateRoot
    {
        // These statuses block a second application for the same (applicant, program, academic year).
        // Rejected, Withdrawn and Expired free the slot. The database filtered unique index uses the same list.
        public static readonly AdmissionApplicationStatus[] FinishedWithoutAdmission =
            [AdmissionApplicationStatus.Rejected, AdmissionApplicationStatus.Withdrawn, AdmissionApplicationStatus.Expired];

        private readonly List<AdmissionApplicationRequirement> _requirements = [];
        private readonly List<AdmissionDecision> _decisions = [];

        private AdmissionApplication()
        {
        }

        public Guid ApplicantId { get; private set; }
        public Guid AcademicProgramId { get; private set; }
        public Guid AcademicYearId { get; private set; }
        public AdmissionApplicationStatus Status { get; private set; }

        // Copied from the academic year's admission deadline when the application is created.
        public DateTimeOffset Deadline { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? SubmittedAt { get; private set; }
        public DateTimeOffset? ReviewStartedAt { get; private set; }
        public DateTimeOffset? DecidedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public IReadOnlyList<AdmissionApplicationRequirement> Requirements => _requirements.AsReadOnly();
        public IReadOnlyList<AdmissionDecision> Decisions => _decisions.AsReadOnly();

        // Creates a Draft with one requirement line per ACTIVE requirement of the program.
        public static Result<AdmissionApplication> Create(Guid applicantId, Guid academicProgramId, Guid academicYearId, DateTimeOffset deadline, IReadOnlyCollection<AdmissionRequirement> programRequirements, DateTimeOffset now)
        {
            if (applicantId == Guid.Empty || academicProgramId == Guid.Empty || academicYearId == Guid.Empty)
            {
                return AdmissionsErrors.InvalidReference;
            }

            if (deadline <= now)
            {
                return AdmissionsErrors.Expired;
            }

            var application = new AdmissionApplication
            {
                Id = Guid.NewGuid(),
                ApplicantId = applicantId,
                AcademicProgramId = academicProgramId,
                AcademicYearId = academicYearId,
                Status = AdmissionApplicationStatus.Draft,
                Deadline = deadline,
                CreatedAt = now
            };

            foreach (var requirement in programRequirements.Where(r => r.IsActive && r.AcademicProgramId == academicProgramId))
            {
                application._requirements.Add(AdmissionApplicationRequirement.CopyOf(application.Id, requirement));
            }

            return application;
        }

        // ---------- applicant side ----------

        // Draft: the normal upload. ConditionallyAccepted: the applicant supplies what the condition asked for.
        public Result AttachDocument(Guid applicationRequirementId, Guid documentId, DateTimeOffset now)
        {
            if (Status is not (AdmissionApplicationStatus.Draft or AdmissionApplicationStatus.ConditionallyAccepted))
            {
                return Result.Fail(AdmissionsErrors.InvalidState);
            }

            if (now > Deadline)
            {
                return Result.Fail(AdmissionsErrors.Expired);
            }

            var line = _requirements.FirstOrDefault(r => r.Id == applicationRequirementId);
            if (line is null)
            {
                return Result.Fail(AdmissionsErrors.ApplicationRequirementNotFound);
            }

            var attached = line.AttachDocument(documentId, now);
            if (attached.IsFailure)
            {
                return attached;
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Submit(DateTimeOffset now)
        {
            if (Status != AdmissionApplicationStatus.Draft)
            {
                return Result.Fail(AdmissionsErrors.InvalidState);
            }

            if (now > Deadline)
            {
                return Result.Fail(AdmissionsErrors.Expired);
            }

            if (_requirements.Any(r => r.IsMandatory && !r.HasDocument))
            {
                return Result.Fail(AdmissionsErrors.MandatoryRequirementMissing);
            }

            Status = AdmissionApplicationStatus.Submitted;
            SubmittedAt = now;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Withdraw(DateTimeOffset now)
        {
            if (Status is not (AdmissionApplicationStatus.Draft or AdmissionApplicationStatus.Submitted))
            {
                return Result.Fail(AdmissionsErrors.InvalidState);
            }

            Status = AdmissionApplicationStatus.Withdrawn;
            UpdatedAt = now;
            return Result.Ok();
        }

        // ---------- reviewer side (Admissions.Review) ----------

        public Result StartReview(DateTimeOffset now)
        {
            if (Status != AdmissionApplicationStatus.Submitted)
            {
                return Result.Fail(AdmissionsErrors.InvalidState);
            }

            Status = AdmissionApplicationStatus.UnderReview;
            ReviewStartedAt = now;
            UpdatedAt = now;
            return Result.Ok();
        }

        // Reviewing is allowed while UnderReview, and while ConditionallyAccepted (to check the documents the
        // applicant added to meet the conditions).
        public Result ReviewRequirement(Guid applicationRequirementId, bool approve, string? note, Guid reviewerId, DateTimeOffset now)
        {
            if (Status is not (AdmissionApplicationStatus.UnderReview or AdmissionApplicationStatus.ConditionallyAccepted))
            {
                return Result.Fail(AdmissionsErrors.InvalidState);
            }

            var line = _requirements.FirstOrDefault(r => r.Id == applicationRequirementId);
            if (line is null)
            {
                return Result.Fail(AdmissionsErrors.ApplicationRequirementNotFound);
            }

            var reviewed = line.Review(approve, note, reviewerId, now);
            if (reviewed.IsFailure)
            {
                return reviewed;
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        // ---------- decision side (Admissions.Decide) ----------

        // Accept: every mandatory requirement is Approved. Raises AdmissionApplicationAccepted.
        public Result Accept(string? reason, Guid decidedBy, DateTimeOffset now)
        {
            if (Status != AdmissionApplicationStatus.UnderReview)
            {
                return Result.Fail(AdmissionsErrors.InvalidState);
            }

            return Admit(reason, decidedBy, now);
        }

        // ConditionallyAccept: at least one mandatory requirement is still not Approved; that is the condition.
        public Result ConditionallyAccept(string? reason, Guid decidedBy, DateTimeOffset now)
        {
            if (Status != AdmissionApplicationStatus.UnderReview)
            {
                return Result.Fail(AdmissionsErrors.InvalidState);
            }

            if (MandatoryRequirementsApproved())
            {
                return Result.Fail(AdmissionsErrors.NoConditionsPending);
            }

            var decision = AdmissionDecision.Create(Id, AdmissionDecisionType.ConditionallyAccepted, reason, decidedBy, now);
            if (!decision.TryGetValue(out var created))
            {
                return Result.Fail(decision.Errors);
            }

            _decisions.Add(created);
            Status = AdmissionApplicationStatus.ConditionallyAccepted;
            DecidedAt = now;
            UpdatedAt = now;
            return Result.Ok();
        }

        // ConditionallyAccepted -> Accepted once every mandatory requirement is Approved.
        public Result CompleteConditions(string? reason, Guid decidedBy, DateTimeOffset now)
        {
            if (Status != AdmissionApplicationStatus.ConditionallyAccepted)
            {
                return Result.Fail(AdmissionsErrors.InvalidState);
            }

            if (now > Deadline)
            {
                return Result.Fail(AdmissionsErrors.Expired);
            }

            if (!MandatoryRequirementsApproved())
            {
                return Result.Fail(AdmissionsErrors.ConditionsPending);
            }

            return Admit(reason, decidedBy, now);
        }

        public Result Reject(string? reason, Guid decidedBy, DateTimeOffset now)
        {
            if (Status != AdmissionApplicationStatus.UnderReview)
            {
                return Result.Fail(AdmissionsErrors.InvalidState);
            }

            var decision = AdmissionDecision.Create(Id, AdmissionDecisionType.Rejected, reason, decidedBy, now);
            if (!decision.TryGetValue(out var created))
            {
                return Result.Fail(decision.Errors);
            }

            _decisions.Add(created);
            Status = AdmissionApplicationStatus.Rejected;
            DecidedAt = now;
            UpdatedAt = now;
            return Result.Ok();
        }

        // ---------- system side ----------

        // Called by the expiry job. Not allowed before the deadline.
        public Result Expire(DateTimeOffset now)
        {
            if (Status is not (AdmissionApplicationStatus.Draft or AdmissionApplicationStatus.Submitted
                or AdmissionApplicationStatus.UnderReview or AdmissionApplicationStatus.ConditionallyAccepted))
            {
                return Result.Fail(AdmissionsErrors.InvalidState);
            }

            if (now <= Deadline)
            {
                return Result.Fail(AdmissionsErrors.NotExpiredYet);
            }

            Status = AdmissionApplicationStatus.Expired;
            UpdatedAt = now;
            return Result.Ok();
        }

        // ---------- helpers ----------

        public bool MandatoryRequirementsApproved()
            => _requirements.Where(r => r.IsMandatory).All(r => r.ReviewStatus == RequirementReviewStatus.Approved);

        private Result Admit(string? reason, Guid decidedBy, DateTimeOffset now)
        {
            if (!MandatoryRequirementsApproved())
            {
                return Result.Fail(AdmissionsErrors.MandatoryRequirementNotApproved);
            }

            var decision = AdmissionDecision.Create(Id, AdmissionDecisionType.Accepted, reason, decidedBy, now);
            if (!decision.TryGetValue(out var created))
            {
                return Result.Fail(decision.Errors);
            }

            _decisions.Add(created);
            Status = AdmissionApplicationStatus.Accepted;
            DecidedAt = now;
            UpdatedAt = now;

            RaiseDomainEvent(new AdmissionApplicationAccepted(Guid.NewGuid(), now, Id, ApplicantId, AcademicProgramId, AcademicYearId, created.Id));
            return Result.Ok();
        }
    }
}
