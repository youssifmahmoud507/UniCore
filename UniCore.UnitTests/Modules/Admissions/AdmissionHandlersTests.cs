using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Admissions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Admissions;
using UniCore.Domain.Modules.Identity;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Admissions
{
    public class AdmissionHandlersTests
    {
        private static readonly PaginationQuery FirstPage = new(1, 20);
        private static readonly RequestInfo Req = AdmissionsTestEnv.Request;

        private static CreateApplicantCommand NewApplicant(string email = "mona@example.com")
            => new("Mona Ali", email, "+20 100 123 4567", new DateOnly(2008, 5, 20));

        // A program with a mandatory CERT, a mandatory ID and an optional PHOTO.
        private static (AdmissionsTestEnv Env, Guid ProgramId, Guid ApplicantId) NewWorld()
        {
            var env = new AdmissionsTestEnv();
            var program = env.SeedProgram();
            env.SeedRequirement(program.Id, "CERT");
            env.SeedRequirement(program.Id, "ID");
            env.SeedRequirement(program.Id, "PHOTO", mandatory: false);
            var applicant = env.SeedApplicant();
            return (env, program.Id, applicant.Id);
        }

        private static Guid LineId(ApplicationDetail detail, string code) => detail.Requirements.Single(r => r.Code == code).Id;

        private static async Task<ApplicationDetail> ReadyForReviewAsync(AdmissionsTestEnv env, Guid applicantId, Guid programId)
        {
            var draft = await env.CreateDraftAsync(applicantId, programId);
            var id = draft.Application.Id;

            foreach (var code in new[] { "CERT", "ID" })
            {
                ResultAssert.Succeeded(await env.ApplicationHandler.AttachDocumentAsync(id, LineId(draft, code), new AttachDocumentCommand(Guid.NewGuid()), Req));
            }

            ResultAssert.Succeeded(await env.ApplicationHandler.SubmitAsync(id, Req));
            return ResultAssert.Succeeded(await env.ApplicationHandler.StartReviewAsync(id, Req));
        }

        private static async Task ApproveMandatoryAsync(AdmissionsTestEnv env, ApplicationDetail detail)
        {
            foreach (var code in new[] { "CERT", "ID" })
            {
                ResultAssert.Succeeded(await env.ApplicationHandler.ReviewRequirementAsync(detail.Application.Id, LineId(detail, code), new ReviewRequirementCommand(true, null), Req));
            }
        }

        // ---------- applicants ----------

        [Fact]
        public async Task Applicant_create_saves_once_and_audits_without_personal_data()
        {
            var env = new AdmissionsTestEnv();
            env.AsReviewer();

            var summary = ResultAssert.Succeeded(await env.ApplicantHandler.CreateAsync(NewApplicant(), Req));

            Assert.Equal("mona@example.com", summary.Email);
            Assert.Single(env.Applicants.Items);
            Assert.Equal(1, env.Rbac.Uow.SaveCalls);

            var audit = Assert.Single(env.Rbac.Audit.Entries);
            Assert.Equal(AdmissionsAuditActions.ApplicantCreated, audit.Action);
            Assert.Equal(summary.Id.ToString(), audit.EntityId);
            Assert.Null(audit.OldValues);
            Assert.Null(audit.NewValues);
        }

        [Fact]
        public async Task Applicant_create_rejects_a_duplicate_email_and_a_missing_birth_date()
        {
            var env = new AdmissionsTestEnv();
            env.SeedApplicant("mona@example.com");
            env.AsReviewer();

            ResultAssert.Failed(await env.ApplicantHandler.CreateAsync(NewApplicant(" MONA@example.com "), Req), AdmissionsErrors.ApplicantEmailExists);
            ResultAssert.Failed(await env.ApplicantHandler.CreateAsync(NewApplicant() with { DateOfBirth = null }, Req), AdmissionsErrors.InvalidDateOfBirth);
            Assert.Empty(env.Rbac.Audit.Entries);
        }

        [Fact]
        public async Task Applicant_create_needs_the_review_permission()
        {
            var env = new AdmissionsTestEnv();
            env.AsOfficer(Permissions.Admissions.Read);

            ResultAssert.Failed(await env.ApplicantHandler.CreateAsync(NewApplicant(), Req), IdentityErrors.ForbiddenPermissionMissing);
        }

        [Fact]
        public async Task Applicant_update_allows_the_same_email_but_not_another_applicants()
        {
            var env = new AdmissionsTestEnv();
            var mona = env.SeedApplicant("mona@example.com");
            env.SeedApplicant("sara@example.com");
            env.AsReviewer();

            ResultAssert.Succeeded(await env.ApplicantHandler.UpdateAsync(mona.Id, new UpdateApplicantCommand("Mona A. Ali", "mona@example.com", null, new DateOnly(2008, 5, 20)), Req));
            ResultAssert.Failed(await env.ApplicantHandler.UpdateAsync(mona.Id, new UpdateApplicantCommand("Mona A. Ali", "sara@example.com", null, new DateOnly(2008, 5, 20)), Req), AdmissionsErrors.ApplicantEmailExists);
            ResultAssert.Failed(await env.ApplicantHandler.UpdateAsync(Guid.NewGuid(), new UpdateApplicantCommand("Mona A. Ali", "x@example.com", null, new DateOnly(2008, 5, 20)), Req), AdmissionsErrors.ApplicantNotFound);
        }

        [Fact]
        public async Task Applicant_list_and_get_need_the_read_permission()
        {
            var env = new AdmissionsTestEnv();
            var applicant = env.SeedApplicant();
            env.AsOfficer(Permissions.Admissions.Review);

            ResultAssert.Failed(await env.ApplicantHandler.ListAsync(FirstPage, null, null), IdentityErrors.ForbiddenPermissionMissing);
            ResultAssert.Failed(await env.ApplicantHandler.GetAsync(applicant.Id), IdentityErrors.ForbiddenPermissionMissing);

            env.AsReviewer();
            var page = ResultAssert.Succeeded(await env.ApplicantHandler.ListAsync(FirstPage, "mona", "-name"));
            Assert.Equal(1, page.TotalCount);
            Assert.Equal(applicant.Id, ResultAssert.Succeeded(await env.ApplicantHandler.GetAsync(applicant.Id)).Id);
            ResultAssert.Failed(await env.ApplicantHandler.GetAsync(Guid.NewGuid()), AdmissionsErrors.ApplicantNotFound);
            Assert.True((await env.ApplicantHandler.ListAsync(FirstPage, null, "email")).IsFailure);
        }

        // ---------- requirement catalog ----------

        [Fact]
        public async Task Requirement_create_succeeds_audits_and_rejects_a_duplicate_code()
        {
            var env = new AdmissionsTestEnv();
            var program = env.SeedProgram();
            env.AsSuperAdmin();

            var summary = ResultAssert.Succeeded(await env.RequirementHandler.CreateAsync(new CreateRequirementCommand(program.Id, "cert", "Certificate", null, true), Req));

            Assert.Equal("CERT", summary.Code);
            Assert.Equal(AdmissionsAuditActions.RequirementCreated, Assert.Single(env.Rbac.Audit.Entries).Action);
            ResultAssert.Failed(await env.RequirementHandler.CreateAsync(new CreateRequirementCommand(program.Id, "CERT", "Other", null, true), Req), AdmissionsErrors.RequirementCodeExists);
        }

        [Fact]
        public async Task Requirement_create_rejects_unknown_and_inactive_programs()
        {
            var env = new AdmissionsTestEnv();
            var inactive = env.SeedProgram(code: "OLD", active: false);
            env.AsSuperAdmin();

            ResultAssert.Failed(await env.RequirementHandler.CreateAsync(new CreateRequirementCommand(Guid.NewGuid(), "CERT", "Certificate", null, true), Req), AdmissionsErrors.ProgramNotFound);
            ResultAssert.Failed(await env.RequirementHandler.CreateAsync(new CreateRequirementCommand(inactive.Id, "CERT", "Certificate", null, true), Req), AdmissionsErrors.ProgramInactive);
        }

        [Fact]
        public async Task Requirement_changes_are_limited_to_the_deans_faculty()
        {
            var env = new AdmissionsTestEnv();
            var facultyA = Guid.NewGuid();
            var facultyB = Guid.NewGuid();
            var departmentB = Guid.NewGuid();
            env.PlaceDepartmentInFaculty(departmentB, facultyB);
            var program = env.SeedProgram(departmentB);
            var requirement = env.SeedRequirement(program.Id, "CERT");

            env.AsDeanOf(facultyA);

            ResultAssert.Failed(await env.RequirementHandler.CreateAsync(new CreateRequirementCommand(program.Id, "ID", "Identity", null, true), Req), IdentityErrors.ForbiddenOutOfScope);
            ResultAssert.Failed(await env.RequirementHandler.DeactivateAsync(requirement.Id, Req), IdentityErrors.ForbiddenOutOfScope);

            env.AsDeanOf(facultyB);
            ResultAssert.Succeeded(await env.RequirementHandler.DeactivateAsync(requirement.Id, Req));
            Assert.False(requirement.IsActive);
        }

        [Fact]
        public async Task Requirement_update_activate_and_list()
        {
            var env = new AdmissionsTestEnv();
            var program = env.SeedProgram();
            var requirement = env.SeedRequirement(program.Id, "CERT");
            env.SeedRequirement(program.Id, "OLD", active: false);
            env.AsSuperAdmin();

            var updated = ResultAssert.Succeeded(await env.RequirementHandler.UpdateAsync(requirement.Id, new UpdateRequirementCommand("Certificate copy", null, false), Req));
            Assert.False(updated.IsMandatory);

            ResultAssert.Failed(await env.RequirementHandler.ActivateAsync(requirement.Id, Req), AdmissionsErrors.AlreadyActive);
            ResultAssert.Failed(await env.RequirementHandler.UpdateAsync(Guid.NewGuid(), new UpdateRequirementCommand("X", null, true), Req), AdmissionsErrors.RequirementNotFound);

            Assert.Equal(2, ResultAssert.Succeeded(await env.RequirementHandler.ListAsync(program.Id, activeOnly: false)).Count);
            Assert.Single(ResultAssert.Succeeded(await env.RequirementHandler.ListAsync(program.Id, activeOnly: true)));
        }

        [Fact]
        public async Task Requirement_catalog_changes_need_the_decide_permission()
        {
            var env = new AdmissionsTestEnv();
            var program = env.SeedProgram();
            env.AsReviewer();

            ResultAssert.Failed(await env.RequirementHandler.CreateAsync(new CreateRequirementCommand(program.Id, "CERT", "Certificate", null, true), Req), IdentityErrors.ForbiddenPermissionMissing);
        }

        // ---------- creating an application ----------

        [Fact]
        public async Task Application_create_copies_the_active_requirements_and_audits()
        {
            var (env, programId, applicantId) = NewWorld();
            env.SeedRequirement(programId, "OLD", active: false);
            env.AsReviewer();

            var detail = await env.CreateDraftAsync(applicantId, programId);

            Assert.Equal(AdmissionApplicationStatus.Draft, detail.Application.Status);
            Assert.Equal(env.Deadlines.Deadlines[env.YearId], detail.Application.Deadline);
            Assert.Equal(new[] { "CERT", "ID", "PHOTO" }, detail.Requirements.Select(r => r.Code).ToArray());

            var audit = Assert.Single(env.Rbac.Audit.Entries);
            Assert.Equal(AdmissionsAuditActions.ApplicationCreated, audit.Action);
            Assert.Equal(detail.Application.Id.ToString(), audit.EntityId);
        }

        [Fact]
        public async Task Application_create_rejects_bad_references()
        {
            var (env, programId, applicantId) = NewWorld();
            var inactive = env.SeedProgram(code: "OLD", active: false);
            env.AsReviewer();

            ResultAssert.Failed(await env.ApplicationHandler.CreateAsync(new CreateApplicationCommand(Guid.Empty, programId, env.YearId), Req), AdmissionsErrors.InvalidReference);
            ResultAssert.Failed(await env.ApplicationHandler.CreateAsync(new CreateApplicationCommand(applicantId, Guid.NewGuid(), env.YearId), Req), AdmissionsErrors.ProgramNotFound);
            ResultAssert.Failed(await env.ApplicationHandler.CreateAsync(new CreateApplicationCommand(applicantId, inactive.Id, env.YearId), Req), AdmissionsErrors.ProgramInactive);
            ResultAssert.Failed(await env.ApplicationHandler.CreateAsync(new CreateApplicationCommand(Guid.NewGuid(), programId, env.YearId), Req), AdmissionsErrors.ApplicantNotFound);
            ResultAssert.Failed(await env.ApplicationHandler.CreateAsync(new CreateApplicationCommand(applicantId, programId, Guid.NewGuid()), Req), AdmissionsErrors.DeadlineNotConfigured);
            Assert.Empty(env.Applications.Items);
        }

        [Fact]
        public async Task Application_create_fails_when_the_deadline_has_already_passed()
        {
            var (env, programId, applicantId) = NewWorld();
            env.Deadlines.Deadlines[env.YearId] = AdmissionsTestEnv.Start.AddDays(-1);
            env.AsReviewer();

            ResultAssert.Failed(await env.ApplicationHandler.CreateAsync(new CreateApplicationCommand(applicantId, programId, env.YearId), Req), AdmissionsErrors.Expired);
        }

        [Fact]
        public async Task Only_one_active_application_per_applicant_program_and_year()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsReviewer();

            var first = await env.CreateDraftAsync(applicantId, programId);

            ResultAssert.Failed(await env.ApplicationHandler.CreateAsync(new CreateApplicationCommand(applicantId, programId, env.YearId), Req), AdmissionsErrors.DuplicateApplication);

            // A withdrawn application frees the slot.
            ResultAssert.Succeeded(await env.ApplicationHandler.WithdrawAsync(first.Application.Id, Req));
            await env.CreateDraftAsync(applicantId, programId);
            Assert.Equal(2, env.Applications.Items.Count);
        }

        [Fact]
        public async Task Application_create_needs_the_review_permission_and_the_programs_scope()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsOfficer(Permissions.Admissions.Read);
            ResultAssert.Failed(await env.ApplicationHandler.CreateAsync(new CreateApplicationCommand(applicantId, programId, env.YearId), Req), IdentityErrors.ForbiddenPermissionMissing);

            var otherFaculty = Guid.NewGuid();
            env.AsDeanOf(otherFaculty);
            var program = env.Programs.Items.Single();
            env.PlaceDepartmentInFaculty(program.DepartmentId, Guid.NewGuid());
            ResultAssert.Failed(await env.ApplicationHandler.CreateAsync(new CreateApplicationCommand(applicantId, programId, env.YearId), Req), IdentityErrors.ForbiddenOutOfScope);
        }

        // ---------- the whole flow ----------

        [Fact]
        public async Task Full_flow_to_acceptance_audits_every_step_and_raises_the_event()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsSuperAdmin();

            var review = await ReadyForReviewAsync(env, applicantId, programId);
            await ApproveMandatoryAsync(env, review);

            var accepted = ResultAssert.Succeeded(await env.ApplicationHandler.AcceptAsync(review.Application.Id, new DecisionCommand("Excellent grades"), Req));

            Assert.Equal(AdmissionApplicationStatus.Accepted, accepted.Application.Status);
            var decision = Assert.Single(accepted.Decisions);
            Assert.Equal(AdmissionDecisionType.Accepted, decision.Type);
            Assert.Equal(env.ActorId, decision.DecidedBy);

            Assert.Equal(
                new[]
                {
                    AdmissionsAuditActions.ApplicationCreated,
                    AdmissionsAuditActions.DocumentAttached,
                    AdmissionsAuditActions.DocumentAttached,
                    AdmissionsAuditActions.ApplicationSubmitted,
                    AdmissionsAuditActions.ReviewStarted,
                    AdmissionsAuditActions.RequirementApproved,
                    AdmissionsAuditActions.RequirementApproved,
                    AdmissionsAuditActions.ApplicationAccepted
                },
                env.Rbac.Audit.Entries.Select(e => e.Action).ToArray());

            var stored = env.Applications.Items.Single();
            Assert.IsType<AdmissionApplicationAccepted>(Assert.Single(stored.DomainEvents));
        }

        [Fact]
        public async Task Accept_fails_while_a_mandatory_requirement_is_not_approved_and_saves_nothing()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsSuperAdmin();
            var review = await ReadyForReviewAsync(env, applicantId, programId);
            var saves = env.Rbac.Uow.SaveCalls;
            var audits = env.Rbac.Audit.Entries.Count;

            ResultAssert.Failed(await env.ApplicationHandler.AcceptAsync(review.Application.Id, new DecisionCommand(null), Req), AdmissionsErrors.MandatoryRequirementNotApproved);

            Assert.Equal(saves, env.Rbac.Uow.SaveCalls);
            Assert.Equal(audits, env.Rbac.Audit.Entries.Count);
        }

        [Fact]
        public async Task Conditional_acceptance_then_completion()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsSuperAdmin();
            var review = await ReadyForReviewAsync(env, applicantId, programId);
            var id = review.Application.Id;

            ResultAssert.Succeeded(await env.ApplicationHandler.ReviewRequirementAsync(id, LineId(review, "CERT"), new ReviewRequirementCommand(true, null), Req));
            ResultAssert.Succeeded(await env.ApplicationHandler.ReviewRequirementAsync(id, LineId(review, "ID"), new ReviewRequirementCommand(false, "Blurry"), Req));

            var conditional = ResultAssert.Succeeded(await env.ApplicationHandler.ConditionallyAcceptAsync(id, new DecisionCommand("Send a clear ID"), Req));
            Assert.Equal(AdmissionApplicationStatus.ConditionallyAccepted, conditional.Application.Status);

            ResultAssert.Failed(await env.ApplicationHandler.CompleteConditionsAsync(id, new DecisionCommand(null), Req), AdmissionsErrors.ConditionsPending);

            ResultAssert.Succeeded(await env.ApplicationHandler.AttachDocumentAsync(id, LineId(review, "ID"), new AttachDocumentCommand(Guid.NewGuid()), Req));
            ResultAssert.Succeeded(await env.ApplicationHandler.ReviewRequirementAsync(id, LineId(review, "ID"), new ReviewRequirementCommand(true, null), Req));

            var accepted = ResultAssert.Succeeded(await env.ApplicationHandler.CompleteConditionsAsync(id, new DecisionCommand("Conditions met"), Req));
            Assert.Equal(AdmissionApplicationStatus.Accepted, accepted.Application.Status);
            Assert.Equal(2, accepted.Decisions.Count);
        }

        [Fact]
        public async Task Reject_keeps_the_reason_and_audits()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsSuperAdmin();
            var review = await ReadyForReviewAsync(env, applicantId, programId);

            ResultAssert.Failed(await env.ApplicationHandler.RejectAsync(review.Application.Id, new DecisionCommand(null), Req), AdmissionsErrors.ReasonRequired);

            var rejected = ResultAssert.Succeeded(await env.ApplicationHandler.RejectAsync(review.Application.Id, new DecisionCommand("Below the minimum grade"), Req));

            Assert.Equal(AdmissionApplicationStatus.Rejected, rejected.Application.Status);
            Assert.Equal("Below the minimum grade", Assert.Single(rejected.Decisions).Reason);
            Assert.Equal(AdmissionsAuditActions.ApplicationRejected, env.Rbac.Audit.Entries.Last().Action);
        }

        // ---------- permissions ----------

        [Fact]
        public async Task A_reviewer_cannot_decide()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsReviewer();
            var review = await ReadyForReviewAsync(env, applicantId, programId);
            await ApproveMandatoryAsync(env, review);

            ResultAssert.Failed(await env.ApplicationHandler.AcceptAsync(review.Application.Id, new DecisionCommand(null), Req), IdentityErrors.ForbiddenPermissionMissing);
            ResultAssert.Failed(await env.ApplicationHandler.RejectAsync(review.Application.Id, new DecisionCommand("No"), Req), IdentityErrors.ForbiddenPermissionMissing);
        }

        [Fact]
        public async Task Without_the_permission_an_unknown_id_looks_the_same_as_a_real_one()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsSuperAdmin();
            var draft = await env.CreateDraftAsync(applicantId, programId);

            env.AsOfficer(Permissions.Admissions.Read);

            var real = await env.ApplicationHandler.SubmitAsync(draft.Application.Id, Req);
            var unknown = await env.ApplicationHandler.SubmitAsync(Guid.NewGuid(), Req);

            ResultAssert.Failed(real, IdentityErrors.ForbiddenPermissionMissing);
            ResultAssert.Failed(unknown, IdentityErrors.ForbiddenPermissionMissing);
        }

        [Fact]
        public async Task A_dean_of_another_faculty_cannot_read_or_change_the_application()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsSuperAdmin();
            var draft = await env.CreateDraftAsync(applicantId, programId);

            var departmentId = env.Programs.Items.Single().DepartmentId;
            var home = Guid.NewGuid();
            env.PlaceDepartmentInFaculty(departmentId, home);

            env.AsDeanOf(Guid.NewGuid());
            ResultAssert.Failed(await env.ApplicationHandler.GetAsync(draft.Application.Id), IdentityErrors.ForbiddenOutOfScope);
            ResultAssert.Failed(await env.ApplicationHandler.WithdrawAsync(draft.Application.Id, Req), IdentityErrors.ForbiddenOutOfScope);

            env.AsDeanOf(home);
            ResultAssert.Succeeded(await env.ApplicationHandler.GetAsync(draft.Application.Id));
        }

        [Fact]
        public async Task Not_signed_in_is_unauthorized()
        {
            var env = new AdmissionsTestEnv();

            var result = await env.ApplicationHandler.GetAsync(Guid.NewGuid());

            Assert.Equal(ErrorType.Unauthorized, result.Error?.ErrorType);
        }

        // ---------- documents, review input, failures ----------

        [Fact]
        public async Task Attach_document_rejects_unknown_documents_and_empty_ids()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsReviewer();
            var draft = await env.CreateDraftAsync(applicantId, programId);
            var missing = Guid.NewGuid();
            env.Documents.Unknown.Add(missing);

            ResultAssert.Failed(await env.ApplicationHandler.AttachDocumentAsync(draft.Application.Id, LineId(draft, "CERT"), new AttachDocumentCommand(missing), Req), AdmissionsErrors.DocumentNotFound);
            ResultAssert.Failed(await env.ApplicationHandler.AttachDocumentAsync(draft.Application.Id, LineId(draft, "CERT"), new AttachDocumentCommand(Guid.Empty), Req), AdmissionsErrors.DocumentInvalid);
        }

        [Fact]
        public async Task Submit_fails_until_every_mandatory_document_is_attached()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsReviewer();
            var draft = await env.CreateDraftAsync(applicantId, programId);

            ResultAssert.Failed(await env.ApplicationHandler.SubmitAsync(draft.Application.Id, Req), AdmissionsErrors.MandatoryRequirementMissing);
        }

        [Fact]
        public async Task Review_requires_an_explicit_approve_or_reject()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsReviewer();
            var review = await ReadyForReviewAsync(env, applicantId, programId);

            ResultAssert.Failed(await env.ApplicationHandler.ReviewRequirementAsync(review.Application.Id, LineId(review, "CERT"), new ReviewRequirementCommand(null, null), Req), AdmissionsErrors.ReviewDecisionRequired);
            ResultAssert.Failed(await env.ApplicationHandler.ReviewRequirementAsync(review.Application.Id, LineId(review, "CERT"), new ReviewRequirementCommand(false, null), Req), AdmissionsErrors.ReviewNoteRequired);
        }

        [Fact]
        public async Task A_failed_save_returns_the_error_and_writes_no_audit()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsReviewer();
            var draft = await env.CreateDraftAsync(applicantId, programId);
            var audits = env.Rbac.Audit.Entries.Count;

            env.Rbac.Uow.FailWith = CommonErrors.DatabaseUpdateFailed;

            ResultAssert.Failed(await env.ApplicationHandler.WithdrawAsync(draft.Application.Id, Req), CommonErrors.DatabaseUpdateFailed);
            Assert.Equal(audits, env.Rbac.Audit.Entries.Count);
        }

        // ---------- queries ----------

        [Fact]
        public async Task List_filters_by_status_and_validates_the_sort()
        {
            var (env, programId, applicantId) = NewWorld();
            var second = env.SeedApplicant("sara@example.com");
            env.AsReviewer();

            var first = await env.CreateDraftAsync(applicantId, programId);
            await env.CreateDraftAsync(second.Id, programId);
            ResultAssert.Succeeded(await env.ApplicationHandler.WithdrawAsync(first.Application.Id, Req));

            var all = ResultAssert.Succeeded(await env.ApplicationHandler.ListAsync(FirstPage, "-createdAt", null, null, null, null));
            Assert.Equal(2, all.TotalCount);

            var drafts = ResultAssert.Succeeded(await env.ApplicationHandler.ListAsync(FirstPage, null, AdmissionApplicationStatus.Draft, programId, env.YearId, second.Id));
            Assert.Equal(1, drafts.TotalCount);

            Assert.True((await env.ApplicationHandler.ListAsync(FirstPage, "status", null, null, null, null)).IsFailure);
        }

        [Fact]
        public async Task Get_returns_the_detail_and_not_found_for_an_unknown_id()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsSuperAdmin();
            var draft = await env.CreateDraftAsync(applicantId, programId);

            var detail = ResultAssert.Succeeded(await env.ApplicationHandler.GetAsync(draft.Application.Id));
            Assert.Equal(3, detail.Requirements.Count);

            ResultAssert.Failed(await env.ApplicationHandler.GetAsync(Guid.NewGuid()), AdmissionsErrors.ApplicationNotFound);
        }

        // ---------- expiry job ----------

        [Fact]
        public async Task Expiry_service_expires_only_overdue_open_applications_and_audits_them()
        {
            var (env, programId, applicantId) = NewWorld();
            var second = env.SeedApplicant("sara@example.com");
            var third = env.SeedApplicant("omar@example.com");
            env.AsSuperAdmin();

            var normal = await env.CreateDraftAsync(applicantId, programId);
            var finished = await env.CreateDraftAsync(second.Id, programId);
            ResultAssert.Succeeded(await env.ApplicationHandler.WithdrawAsync(finished.Application.Id, Req));
            var open = await env.CreateDraftAsync(third.Id, programId);

            // Move one application's deadline into the past by creating it under another year.
            var pastYear = Guid.NewGuid();
            env.Deadlines.Deadlines[pastYear] = AdmissionsTestEnv.Start.AddDays(5);
            var late = ResultAssert.Succeeded(await env.ApplicationHandler.CreateAsync(new CreateApplicationCommand(applicantId, programId, pastYear), Req));
            env.Rbac.Audit.Entries.Clear();

            env.Clock.Advance(TimeSpan.FromDays(10));

            var outcome = ResultAssert.Succeeded(await env.ExpiryService.RunAsync());

            Assert.Equal(1, outcome.Expired);
            Assert.Equal(0, outcome.Failed);

            var expired = env.Applications.Items.Single(a => a.Id == late.Application.Id);
            Assert.Equal(AdmissionApplicationStatus.Expired, expired.Status);
            Assert.Equal(AdmissionApplicationStatus.Draft, env.Applications.Items.Single(a => a.Id == normal.Application.Id).Status);
            Assert.Equal(AdmissionApplicationStatus.Draft, env.Applications.Items.Single(a => a.Id == open.Application.Id).Status);
            Assert.Equal(AdmissionApplicationStatus.Withdrawn, env.Applications.Items.Single(a => a.Id == finished.Application.Id).Status);

            var audit = Assert.Single(env.Rbac.Audit.Entries);
            Assert.Equal(AdmissionsAuditActions.ApplicationExpired, audit.Action);
            Assert.Null(audit.ActorUserId);
            Assert.Equal(late.Application.Id.ToString(), audit.EntityId);

            // Running again finds nothing new.
            var again = ResultAssert.Succeeded(await env.ExpiryService.RunAsync());
            Assert.Equal(0, again.Expired);
        }

        [Fact]
        public async Task Expiry_service_returns_the_error_when_the_save_fails()
        {
            var (env, programId, applicantId) = NewWorld();
            env.AsSuperAdmin();
            await env.CreateDraftAsync(applicantId, programId);
            env.Clock.Advance(TimeSpan.FromDays(90));
            env.Rbac.Uow.FailWith = CommonErrors.DatabaseTimeout;

            ResultAssert.Failed(await env.ExpiryService.RunAsync(), CommonErrors.DatabaseTimeout);
        }
    }
}
