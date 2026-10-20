using UniCore.Domain.Modules.Admissions;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Admissions
{
    public class AdmissionApplicationTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Deadline = Now.AddDays(30);
        private static readonly Guid ApplicantId = Guid.NewGuid();
        private static readonly Guid ProgramId = Guid.NewGuid();
        private static readonly Guid YearId = Guid.NewGuid();
        private static readonly Guid Officer = Guid.NewGuid();

        private static AdmissionRequirement Requirement(string code, bool mandatory = true, Guid? programId = null, bool active = true)
        {
            var requirement = ResultAssert.Succeeded(AdmissionRequirement.Create(programId ?? ProgramId, code, "Requirement " + code, null, mandatory, Now));

            if (!active)
            {
                ResultAssert.Succeeded(requirement.Deactivate(Now));
            }

            return requirement;
        }

        // Two mandatory requirements (CERT, ID) and one optional (PHOTO).
        private static AdmissionApplication NewApplication(DateTimeOffset? deadline = null)
            => ResultAssert.Succeeded(AdmissionApplication.Create(ApplicantId, ProgramId, YearId, deadline ?? Deadline,
                [Requirement("CERT"), Requirement("ID"), Requirement("PHOTO", mandatory: false)], Now));

        private static AdmissionApplicationRequirement Line(AdmissionApplication a, string code) => a.Requirements.Single(r => r.Code == code);

        private static void AttachAllMandatory(AdmissionApplication a)
        {
            ResultAssert.Succeeded(a.AttachDocument(Line(a, "CERT").Id, Guid.NewGuid(), Now));
            ResultAssert.Succeeded(a.AttachDocument(Line(a, "ID").Id, Guid.NewGuid(), Now));
        }

        private static AdmissionApplication UnderReview()
        {
            var a = NewApplication();
            AttachAllMandatory(a);
            ResultAssert.Succeeded(a.Submit(Now));
            ResultAssert.Succeeded(a.StartReview(Now));
            return a;
        }

        private static void ApproveAllMandatory(AdmissionApplication a)
        {
            ResultAssert.Succeeded(a.ReviewRequirement(Line(a, "CERT").Id, true, null, Officer, Now));
            ResultAssert.Succeeded(a.ReviewRequirement(Line(a, "ID").Id, true, null, Officer, Now));
        }

        // ---------- create ----------

        [Fact]
        public void Create_makes_a_draft_with_one_line_per_active_requirement_of_the_program()
        {
            var a = ResultAssert.Succeeded(AdmissionApplication.Create(ApplicantId, ProgramId, YearId, Deadline,
                [Requirement("CERT"), Requirement("OLD", active: false), Requirement("OTHER", programId: Guid.NewGuid())], Now));

            Assert.Equal(AdmissionApplicationStatus.Draft, a.Status);
            Assert.Equal(Deadline, a.Deadline);
            var line = Assert.Single(a.Requirements);
            Assert.Equal("CERT", line.Code);
            Assert.Equal(RequirementReviewStatus.Pending, line.ReviewStatus);
            Assert.Null(line.DocumentId);
            Assert.Empty(a.Decisions);
            Assert.Empty(a.DomainEvents);
        }

        [Fact]
        public void Create_rejects_empty_ids_and_a_deadline_that_has_passed()
        {
            ResultAssert.Failed(AdmissionApplication.Create(Guid.Empty, ProgramId, YearId, Deadline, [], Now), AdmissionsErrors.InvalidReference);
            ResultAssert.Failed(AdmissionApplication.Create(ApplicantId, Guid.Empty, YearId, Deadline, [], Now), AdmissionsErrors.InvalidReference);
            ResultAssert.Failed(AdmissionApplication.Create(ApplicantId, ProgramId, Guid.Empty, Deadline, [], Now), AdmissionsErrors.InvalidReference);
            ResultAssert.Failed(AdmissionApplication.Create(ApplicantId, ProgramId, YearId, Now, [], Now), AdmissionsErrors.Expired);
        }

        // ---------- documents and submit ----------

        [Fact]
        public void Attach_document_links_it_and_resets_the_review()
        {
            var a = NewApplication();
            var line = Line(a, "CERT");
            var first = Guid.NewGuid();

            ResultAssert.Succeeded(a.AttachDocument(line.Id, first, Now));

            Assert.Equal(first, line.DocumentId);
            Assert.Equal(Now, line.DocumentAttachedAt);
            Assert.Equal(RequirementReviewStatus.Pending, line.ReviewStatus);
        }

        [Fact]
        public void Attach_document_rejects_empty_id_unknown_line_and_wrong_state()
        {
            var a = NewApplication();

            ResultAssert.Failed(a.AttachDocument(Line(a, "CERT").Id, Guid.Empty, Now), AdmissionsErrors.DocumentInvalid);
            ResultAssert.Failed(a.AttachDocument(Guid.NewGuid(), Guid.NewGuid(), Now), AdmissionsErrors.ApplicationRequirementNotFound);

            AttachAllMandatory(a);
            ResultAssert.Succeeded(a.Submit(Now));
            ResultAssert.Failed(a.AttachDocument(Line(a, "CERT").Id, Guid.NewGuid(), Now), AdmissionsErrors.InvalidState);
        }

        [Fact]
        public void Attach_document_after_the_deadline_fails()
        {
            var a = NewApplication();

            ResultAssert.Failed(a.AttachDocument(Line(a, "CERT").Id, Guid.NewGuid(), Deadline.AddSeconds(1)), AdmissionsErrors.Expired);
        }

        [Fact]
        public void Submit_requires_a_document_for_every_mandatory_requirement()
        {
            var a = NewApplication();
            ResultAssert.Succeeded(a.AttachDocument(Line(a, "CERT").Id, Guid.NewGuid(), Now));

            ResultAssert.Failed(a.Submit(Now), AdmissionsErrors.MandatoryRequirementMissing);
            Assert.Equal(AdmissionApplicationStatus.Draft, a.Status);

            ResultAssert.Succeeded(a.AttachDocument(Line(a, "ID").Id, Guid.NewGuid(), Now));
            ResultAssert.Succeeded(a.Submit(Now.AddHours(1)));

            Assert.Equal(AdmissionApplicationStatus.Submitted, a.Status);
            Assert.Equal(Now.AddHours(1), a.SubmittedAt);
        }

        [Fact]
        public void Submit_does_not_need_the_optional_requirement()
        {
            var a = NewApplication();
            AttachAllMandatory(a);

            ResultAssert.Succeeded(a.Submit(Now));
        }

        [Fact]
        public void Submit_after_the_deadline_fails_and_twice_fails()
        {
            var late = NewApplication();
            AttachAllMandatory(late);
            ResultAssert.Failed(late.Submit(Deadline.AddMinutes(1)), AdmissionsErrors.Expired);

            var a = NewApplication();
            AttachAllMandatory(a);
            ResultAssert.Succeeded(a.Submit(Now));
            ResultAssert.Failed(a.Submit(Now), AdmissionsErrors.InvalidState);
        }

        [Fact]
        public void An_application_without_mandatory_requirements_can_be_submitted_empty()
        {
            var a = ResultAssert.Succeeded(AdmissionApplication.Create(ApplicantId, ProgramId, YearId, Deadline, [Requirement("PHOTO", mandatory: false)], Now));

            ResultAssert.Succeeded(a.Submit(Now));
        }

        // ---------- withdraw ----------

        [Fact]
        public void Withdraw_works_from_draft_and_submitted_only()
        {
            var draft = NewApplication();
            ResultAssert.Succeeded(draft.Withdraw(Now));
            Assert.Equal(AdmissionApplicationStatus.Withdrawn, draft.Status);

            var submitted = NewApplication();
            AttachAllMandatory(submitted);
            ResultAssert.Succeeded(submitted.Submit(Now));
            ResultAssert.Succeeded(submitted.Withdraw(Now));

            var review = UnderReview();
            ResultAssert.Failed(review.Withdraw(Now), AdmissionsErrors.InvalidState);
            ResultAssert.Failed(draft.Withdraw(Now), AdmissionsErrors.InvalidState);
        }

        // ---------- review ----------

        [Fact]
        public void Start_review_needs_a_submitted_application()
        {
            var a = NewApplication();
            ResultAssert.Failed(a.StartReview(Now), AdmissionsErrors.InvalidState);

            AttachAllMandatory(a);
            ResultAssert.Succeeded(a.Submit(Now));
            ResultAssert.Succeeded(a.StartReview(Now.AddHours(2)));

            Assert.Equal(AdmissionApplicationStatus.UnderReview, a.Status);
            Assert.Equal(Now.AddHours(2), a.ReviewStartedAt);
        }

        [Fact]
        public void Review_requirement_approves_and_rejects_with_a_note()
        {
            var a = UnderReview();

            ResultAssert.Succeeded(a.ReviewRequirement(Line(a, "CERT").Id, true, " looks good ", Officer, Now));
            var cert = Line(a, "CERT");
            Assert.Equal(RequirementReviewStatus.Approved, cert.ReviewStatus);
            Assert.Equal("looks good", cert.ReviewNote);
            Assert.Equal(Officer, cert.ReviewedBy);

            ResultAssert.Succeeded(a.ReviewRequirement(Line(a, "ID").Id, false, "Blurry scan", Officer, Now));
            Assert.Equal(RequirementReviewStatus.Rejected, Line(a, "ID").ReviewStatus);
        }

        [Fact]
        public void Review_requirement_rejects_without_note_without_document_and_in_wrong_state()
        {
            var a = UnderReview();

            ResultAssert.Failed(a.ReviewRequirement(Line(a, "CERT").Id, false, " ", Officer, Now), AdmissionsErrors.ReviewNoteRequired);
            ResultAssert.Failed(a.ReviewRequirement(Line(a, "PHOTO").Id, true, null, Officer, Now), AdmissionsErrors.RequirementNotReviewable);
            ResultAssert.Failed(a.ReviewRequirement(Guid.NewGuid(), true, null, Officer, Now), AdmissionsErrors.ApplicationRequirementNotFound);
            ResultAssert.Failed(a.ReviewRequirement(Line(a, "CERT").Id, false, new string('x', 1001), Officer, Now), AdmissionsErrors.ReasonTooLong);

            var draft = NewApplication();
            ResultAssert.Failed(draft.ReviewRequirement(Line(draft, "CERT").Id, true, null, Officer, Now), AdmissionsErrors.InvalidState);
        }

        // ---------- accept ----------

        [Fact]
        public void Accept_needs_every_mandatory_requirement_approved()
        {
            var a = UnderReview();
            ResultAssert.Succeeded(a.ReviewRequirement(Line(a, "CERT").Id, true, null, Officer, Now));

            ResultAssert.Failed(a.Accept(null, Officer, Now), AdmissionsErrors.MandatoryRequirementNotApproved);
            Assert.Equal(AdmissionApplicationStatus.UnderReview, a.Status);
            Assert.Empty(a.Decisions);
            Assert.Empty(a.DomainEvents);
        }

        [Fact]
        public void Accept_records_the_decision_and_raises_the_event_once()
        {
            var a = UnderReview();
            ApproveAllMandatory(a);

            ResultAssert.Succeeded(a.Accept(" Strong file ", Officer, Now.AddDays(1)));

            Assert.Equal(AdmissionApplicationStatus.Accepted, a.Status);
            Assert.Equal(Now.AddDays(1), a.DecidedAt);

            var decision = Assert.Single(a.Decisions);
            Assert.Equal(AdmissionDecisionType.Accepted, decision.Type);
            Assert.Equal("Strong file", decision.Reason);
            Assert.Equal(Officer, decision.DecidedBy);

            var accepted = Assert.IsType<AdmissionApplicationAccepted>(Assert.Single(a.DomainEvents));
            Assert.Equal(a.Id, accepted.ApplicationId);
            Assert.Equal(ApplicantId, accepted.ApplicantId);
            Assert.Equal(ProgramId, accepted.AcademicProgramId);
            Assert.Equal(YearId, accepted.AcademicYearId);
            Assert.Equal(decision.Id, accepted.DecisionId);

            // A second accept is refused, so the event cannot be raised again.
            ResultAssert.Failed(a.Accept(null, Officer, Now), AdmissionsErrors.InvalidState);
            Assert.Single(a.DomainEvents);
        }

        [Fact]
        public void Accept_from_the_wrong_state_fails()
        {
            ResultAssert.Failed(NewApplication().Accept(null, Officer, Now), AdmissionsErrors.InvalidState);
        }

        [Fact]
        public void Decisions_need_a_real_decider()
        {
            var a = UnderReview();
            ApproveAllMandatory(a);

            ResultAssert.Failed(a.Accept(null, Guid.Empty, Now), AdmissionsErrors.InvalidReference);
        }

        // ---------- conditional acceptance ----------

        [Fact]
        public void Conditional_accept_needs_a_reason_and_a_pending_condition()
        {
            var a = UnderReview();
            ResultAssert.Succeeded(a.ReviewRequirement(Line(a, "CERT").Id, true, null, Officer, Now));

            ResultAssert.Failed(a.ConditionallyAccept(null, Officer, Now), AdmissionsErrors.ReasonRequired);

            ApproveAllMandatory(a);
            ResultAssert.Failed(a.ConditionallyAccept("Bring the ID", Officer, Now), AdmissionsErrors.NoConditionsPending);
        }

        [Fact]
        public void Conditional_accept_then_complete_conditions_ends_in_accepted()
        {
            var a = UnderReview();
            ResultAssert.Succeeded(a.ReviewRequirement(Line(a, "CERT").Id, true, null, Officer, Now));
            ResultAssert.Succeeded(a.ReviewRequirement(Line(a, "ID").Id, false, "Expired ID", Officer, Now));

            ResultAssert.Succeeded(a.ConditionallyAccept("Provide a valid ID", Officer, Now));
            Assert.Equal(AdmissionApplicationStatus.ConditionallyAccepted, a.Status);
            Assert.Empty(a.DomainEvents);

            // Not yet: the ID is still rejected.
            ResultAssert.Failed(a.CompleteConditions(null, Officer, Now), AdmissionsErrors.ConditionsPending);

            // The applicant adds a new ID, the reviewer approves it.
            ResultAssert.Succeeded(a.AttachDocument(Line(a, "ID").Id, Guid.NewGuid(), Now.AddDays(2)));
            Assert.Equal(RequirementReviewStatus.Pending, Line(a, "ID").ReviewStatus);
            ResultAssert.Succeeded(a.ReviewRequirement(Line(a, "ID").Id, true, null, Officer, Now.AddDays(3)));

            ResultAssert.Succeeded(a.CompleteConditions("Conditions met", Officer, Now.AddDays(3)));

            Assert.Equal(AdmissionApplicationStatus.Accepted, a.Status);
            Assert.Equal(new[] { AdmissionDecisionType.ConditionallyAccepted, AdmissionDecisionType.Accepted }, a.Decisions.Select(d => d.Type).ToArray());
            Assert.IsType<AdmissionApplicationAccepted>(Assert.Single(a.DomainEvents));
        }

        [Fact]
        public void Complete_conditions_after_the_deadline_fails()
        {
            var a = UnderReview();
            ResultAssert.Succeeded(a.ConditionallyAccept("Bring the documents", Officer, Now));
            ResultAssert.Failed(a.CompleteConditions(null, Officer, Deadline.AddDays(1)), AdmissionsErrors.Expired);
        }

        [Fact]
        public void Complete_conditions_from_the_wrong_state_fails()
        {
            ResultAssert.Failed(UnderReview().CompleteConditions(null, Officer, Now), AdmissionsErrors.InvalidState);
        }

        // ---------- reject ----------

        [Fact]
        public void Reject_needs_a_reason_and_keeps_it()
        {
            var a = UnderReview();

            ResultAssert.Failed(a.Reject(" ", Officer, Now), AdmissionsErrors.ReasonRequired);
            ResultAssert.Failed(a.Reject(new string('x', 1001), Officer, Now), AdmissionsErrors.ReasonTooLong);

            ResultAssert.Succeeded(a.Reject("Below the minimum grade", Officer, Now));

            Assert.Equal(AdmissionApplicationStatus.Rejected, a.Status);
            var decision = Assert.Single(a.Decisions);
            Assert.Equal(AdmissionDecisionType.Rejected, decision.Type);
            Assert.Equal("Below the minimum grade", decision.Reason);
            Assert.Empty(a.DomainEvents);
            ResultAssert.Failed(a.Reject("Again", Officer, Now), AdmissionsErrors.InvalidState);
        }

        // ---------- expire ----------

        [Fact]
        public void Expire_works_for_every_open_state_but_only_after_the_deadline()
        {
            var draft = NewApplication();
            ResultAssert.Failed(draft.Expire(Deadline), AdmissionsErrors.NotExpiredYet);
            ResultAssert.Succeeded(draft.Expire(Deadline.AddSeconds(1)));
            Assert.Equal(AdmissionApplicationStatus.Expired, draft.Status);

            var review = UnderReview();
            ResultAssert.Succeeded(review.Expire(Deadline.AddDays(1)));

            var conditional = UnderReview();
            ResultAssert.Succeeded(conditional.ConditionallyAccept("Bring the documents", Officer, Now));
            ResultAssert.Succeeded(conditional.Expire(Deadline.AddDays(1)));
        }

        [Fact]
        public void Expire_does_not_touch_finished_applications()
        {
            var accepted = UnderReview();
            ApproveAllMandatory(accepted);
            ResultAssert.Succeeded(accepted.Accept(null, Officer, Now));
            ResultAssert.Failed(accepted.Expire(Deadline.AddDays(1)), AdmissionsErrors.InvalidState);

            var withdrawn = NewApplication();
            ResultAssert.Succeeded(withdrawn.Withdraw(Now));
            ResultAssert.Failed(withdrawn.Expire(Deadline.AddDays(1)), AdmissionsErrors.InvalidState);
        }

        [Fact]
        public void Finished_without_admission_lists_exactly_the_statuses_that_free_the_slot()
        {
            Assert.Equal(
                new[] { AdmissionApplicationStatus.Rejected, AdmissionApplicationStatus.Withdrawn, AdmissionApplicationStatus.Expired },
                AdmissionApplication.FinishedWithoutAdmission);
        }
    }
}
