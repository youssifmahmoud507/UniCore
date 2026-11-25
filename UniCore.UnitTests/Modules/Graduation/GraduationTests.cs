using UniCore.Application.Modules.Graduation;
using UniCore.Application.Modules.Transcripts;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Graduation;
using UniCore.Domain.Modules.Students;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Graduation
{
    public class GraduationDomainTests
    {
        private static readonly DateTimeOffset Now = new(2027, 6, 20, 9, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Today = new(2027, 6, 20);

        private static GraduationApplication Submitted() => ResultAssert.Succeeded(GraduationApplication.Submit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now));

        [Fact]
        public void Submit_needs_the_student_the_program_and_the_plan_version()
        {
            ResultAssert.Failed(GraduationApplication.Submit(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Now), GraduationErrors.Invalid);
            ResultAssert.Failed(GraduationApplication.Submit(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Now), GraduationErrors.Invalid);
            ResultAssert.Failed(GraduationApplication.Submit(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Now), GraduationErrors.Invalid);
            Assert.Equal(GraduationApplicationStatus.Submitted, Submitted().Status);
        }

        [Fact]
        public void Approve_freezes_the_figures_and_raises_the_event_once()
        {
            var application = Submitted();
            var approver = Guid.NewGuid();

            ResultAssert.Succeeded(application.Approve(approver, 3.4m, 132, AcademicStanding.GoodStanding, Today, " well done ", Now));

            Assert.Equal(GraduationApplicationStatus.Approved, application.Status);
            Assert.Equal(3.4m, application.CumulativeGpa);
            Assert.Equal(132, application.EarnedCredits);
            Assert.Equal("well done", application.DecisionNote);
            var raised = Assert.Single(application.DomainEvents.OfType<GraduationApproved>());
            Assert.Equal(application.StudentId, raised.StudentId);
            Assert.Equal(approver, raised.ApprovedByUserId);
            Assert.Equal(Today, raised.GraduationDate);
        }

        [Fact]
        public void A_decided_application_cannot_be_decided_or_cancelled_again()
        {
            var approved = Submitted();
            ResultAssert.Succeeded(approved.Approve(Guid.NewGuid(), 3m, 120, null, Today, null, Now));
            ResultAssert.Failed(approved.Approve(Guid.NewGuid(), 3m, 120, null, Today, null, Now), GraduationErrors.InvalidState);
            ResultAssert.Failed(approved.Reject(Guid.NewGuid(), "no", Now), GraduationErrors.InvalidState);
            ResultAssert.Failed(approved.Cancel(Now), GraduationErrors.InvalidState);
            Assert.Single(approved.DomainEvents);

            var rejected = Submitted();
            ResultAssert.Succeeded(rejected.Reject(Guid.NewGuid(), "Missing documents", Now));
            ResultAssert.Failed(rejected.Cancel(Now), GraduationErrors.InvalidState);
        }

        [Fact]
        public void Reject_needs_a_reason_and_the_student_can_cancel_only_while_submitted()
        {
            var application = Submitted();

            ResultAssert.Failed(application.Reject(Guid.NewGuid(), "  ", Now), GraduationErrors.ReasonRequired);
            ResultAssert.Failed(application.Reject(Guid.Empty, "reason", Now), GraduationErrors.Invalid);
            ResultAssert.Failed(application.Reject(Guid.NewGuid(), new string('x', GraduationApplication.NoteMaxLength + 1), Now), GraduationErrors.Invalid);

            ResultAssert.Succeeded(application.Cancel(Now));
            Assert.Equal(GraduationApplicationStatus.Cancelled, application.Status);
            Assert.False(application.IsActive);
        }
    }

    public class GraduationEligibilityTests
    {
        private static IEnumerable<string> Codes(GraduationEligibility e) => e.Unmet.Select(u => u.Code);

        [Fact]
        public async Task A_student_who_meets_every_requirement_is_eligible()
        {
            var env = new GraduationTestEnv();
            var student = env.SeedStudent();

            var result = ResultAssert.Succeeded(await env.Eligibility.CheckAsync(student.Id));

            Assert.True(result.IsEligible);
            Assert.Empty(result.Unmet);
            Assert.Empty(result.MissingRequiredCourses);
        }

        [Fact]
        public async Task A_missing_required_course_is_named_and_an_unpassed_elective_does_not_matter()
        {
            var env = new GraduationTestEnv();
            var student = env.SeedStudent();
            env.Gpa.Passed.Remove(env.Cs102.CourseId);

            var result = ResultAssert.Succeeded(await env.Eligibility.CheckAsync(student.Id));

            Assert.False(result.IsEligible);
            Assert.Equal(new[] { GraduationErrors.RequiredCoursesMissing.Code }, Codes(result).ToArray());
            Assert.Equal(new[] { "CS102" }, result.MissingRequiredCourses.ToArray());
            Assert.Contains("CS102", result.Unmet.Single().Description);
        }

        [Fact]
        public async Task Each_unmet_requirement_has_its_own_code_and_they_are_all_reported_together()
        {
            var env = new GraduationTestEnv();
            var student = env.SeedStudent();
            env.Gpa.Passed.Clear();
            env.Gpa.Figures = new GpaFigures(1.6m, 3, 0, AcademicStanding.Warning);

            var result = ResultAssert.Succeeded(await env.Eligibility.CheckAsync(student.Id));

            Assert.Equal(
                new[] { GraduationErrors.RequiredCoursesMissing.Code, GraduationErrors.CreditsInsufficient.Code, GraduationErrors.GpaBelowMinimum.Code },
                Codes(result).ToArray());
            Assert.Equal(new[] { "CS101", "CS102" }, result.MissingRequiredCourses.ToArray());
        }

        [Fact]
        public async Task The_gpa_minimum_is_inclusive_and_a_student_with_no_graded_credits_is_below_it()
        {
            var env = new GraduationTestEnv();
            var student = env.SeedStudent();

            env.Gpa.Figures = new GpaFigures(2.0m, 6, 6, AcademicStanding.GoodStanding);
            Assert.True(ResultAssert.Succeeded(await env.Eligibility.CheckAsync(student.Id)).IsEligible);

            env.Gpa.Figures = new GpaFigures(1.99m, 6, 6, AcademicStanding.Warning);
            Assert.Equal(new[] { GraduationErrors.GpaBelowMinimum.Code }, Codes(ResultAssert.Succeeded(await env.Eligibility.CheckAsync(student.Id))).ToArray());

            env.Gpa.Figures = new GpaFigures(0m, 0, 0, null);
            Assert.Contains(GraduationErrors.GpaBelowMinimum.Code, Codes(ResultAssert.Succeeded(await env.Eligibility.CheckAsync(student.Id))));
        }

        [Fact]
        public async Task The_study_plan_version_the_student_follows_is_the_one_that_is_read()
        {
            var env = new GraduationTestEnv();
            var olderVersion = Guid.NewGuid();
            env.Plans.Versions[olderVersion] = [new UniCore.Application.Modules.Curriculum.PlanCourseInfo(env.Cs101.CourseVersionId, UniCore.Domain.Modules.Curriculum.CourseRequirementType.Required, null, null, null)];
            var student = env.SeedStudent(olderVersion);
            env.Gpa.Passed.Remove(env.Cs102.CourseId);

            var result = ResultAssert.Succeeded(await env.Eligibility.CheckAsync(student.Id));

            // The older version does not require CS102, so its absence does not matter.
            Assert.Equal(olderVersion, env.Plans.LastVersionRead);
            Assert.True(result.IsEligible);
        }

        [Fact]
        public async Task A_student_who_is_not_active_has_nothing_else_checked()
        {
            var env = new GraduationTestEnv();
            var student = env.SeedStudent();
            ResultAssert.Succeeded(student.ChangeStatus(StudentAcademicStatus.Suspended, new DateOnly(2027, 6, 1), "Disciplinary", Guid.NewGuid(), new DateOnly(2027, 6, 20), env.Clock.UtcNow));

            var result = ResultAssert.Succeeded(await env.Eligibility.CheckAsync(student.Id));

            Assert.False(result.IsEligible);
            Assert.Equal(new[] { GraduationErrors.StudentNotActive.Code }, Codes(result).ToArray());
        }

        [Fact]
        public async Task An_unknown_student_is_not_found()
        {
            var env = new GraduationTestEnv();

            ResultAssert.Failed(await env.Eligibility.CheckAsync(Guid.NewGuid()), GraduationErrors.StudentNotFound);
        }
    }

    public class GraduationHandlerTests
    {
        private static readonly UniCore.Application.Common.Abstractions.RequestInfo Request = GraduationTestEnv.Request;

        private static void AssertForbidden<T>(Result<T> result)
        {
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Forbidden, result.Error?.ErrorType);
        }

        private static async Task<(GraduationTestEnv Env, Student Student, GraduationApplicationSummary Application)> AppliedAsync()
        {
            var env = new GraduationTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);
            var application = ResultAssert.Succeeded(await env.Handler.ApplyAsync(student.Id, Request));

            return (env, student, application);
        }

        // ---------- applying ----------

        [Fact]
        public async Task An_eligible_student_applies_and_it_is_audited()
        {
            var (env, student, application) = await AppliedAsync();

            Assert.Equal(GraduationApplicationStatus.Submitted, application.Status);
            Assert.Equal(env.PlanVersionId, application.StudyPlanVersionId);
            Assert.Single(env.Applications.Items);
            Assert.Equal(1, env.Rbac.Uow.SaveCalls);
            var audit = Assert.Single(env.Rbac.Audit.Entries, e => e.Action == GraduationAuditActions.Applied);
            Assert.Equal(student.UserId, audit.ActorUserId);
        }

        [Fact]
        public async Task An_ineligible_student_cannot_apply_and_gets_the_code_of_every_unmet_requirement()
        {
            var env = new GraduationTestEnv();
            var student = env.SeedStudent();
            env.Gpa.Passed.Clear();
            env.AsStudent(student);

            var result = await env.Handler.ApplyAsync(student.Id, Request);

            Assert.True(result.IsFailure);
            Assert.Contains(GraduationErrors.RequiredCoursesMissing.Code, result.Errors.Select(e => e.Code));
            Assert.Empty(env.Applications.Items);
            Assert.Equal(0, env.Rbac.Uow.SaveCalls);
        }

        [Fact]
        public async Task A_second_active_application_is_rejected_but_a_cancelled_one_can_be_replaced()
        {
            var (env, student, first) = await AppliedAsync();

            ResultAssert.Failed(await env.Handler.ApplyAsync(student.Id, Request), GraduationErrors.AlreadyApplied);

            ResultAssert.Succeeded(await env.Handler.CancelAsync(first.Id, Request));
            ResultAssert.Succeeded(await env.Handler.ApplyAsync(student.Id, Request));
            Assert.Equal(2, env.Applications.Items.Count);
        }

        [Fact]
        public async Task A_student_applies_only_for_themselves()
        {
            var env = new GraduationTestEnv();
            var me = env.SeedStudent();
            var other = env.SeedStudent();
            env.AsStudent(me);

            AssertForbidden(await env.Handler.ApplyAsync(other.Id, Request));
            Assert.Empty(env.Applications.Items);
        }

        [Fact]
        public async Task Staff_without_the_apply_permission_cannot_apply_for_a_student()
        {
            var env = new GraduationTestEnv();
            var student = env.SeedStudent();
            env.AsRegistrar();

            AssertForbidden(await env.Handler.ApplyAsync(student.Id, Request));
        }

        // ---------- reading ----------

        [Fact]
        public async Task A_student_reads_their_own_application_and_eligibility_but_not_another_students()
        {
            var (env, student, application) = await AppliedAsync();
            var other = env.SeedStudent();

            Assert.True((await env.Handler.GetAsync(application.Id)).IsSuccess);
            Assert.True((await env.Handler.GetEligibilityAsync(student.Id)).IsSuccess);
            Assert.Single(ResultAssert.Succeeded(await env.Handler.ListOfStudentAsync(student.Id)));

            env.AsStudent(other);
            AssertForbidden(await env.Handler.GetAsync(application.Id));
            AssertForbidden(await env.Handler.GetEligibilityAsync(student.Id));
            AssertForbidden(await env.Handler.ListOfStudentAsync(student.Id));
        }

        [Fact]
        public async Task A_department_head_reads_their_departments_students_but_the_full_list_is_for_the_registrar()
        {
            var (env, student, application) = await AppliedAsync();

            env.AsHead();
            Assert.True((await env.Handler.GetAsync(application.Id)).IsSuccess);
            AssertForbidden(await env.Handler.ListAsync(new(1, 10), null));

            env.AsHead(Guid.NewGuid());
            AssertForbidden(await env.Handler.GetAsync(application.Id));
            AssertForbidden(await env.Handler.GetEligibilityAsync(student.Id));

            env.AsRegistrar();
            Assert.Single(ResultAssert.Succeeded(await env.Handler.ListAsync(new(1, 10), GraduationApplicationStatus.Submitted)).Items);
        }

        [Fact]
        public async Task An_unknown_application_is_not_found()
        {
            var (env, _, _) = await AppliedAsync();
            env.AsRegistrar();

            ResultAssert.Failed(await env.Handler.GetAsync(Guid.NewGuid()), GraduationErrors.ApplicationNotFound);
            ResultAssert.Failed(await env.Decisions.ApproveAsync(Guid.NewGuid(), null, Request), GraduationErrors.ApplicationNotFound);
        }

        // ---------- cancelling ----------

        [Fact]
        public async Task Only_the_owner_cancels_and_only_while_submitted()
        {
            var (env, student, application) = await AppliedAsync();
            var other = env.SeedStudent();

            env.AsStudent(other);
            AssertForbidden(await env.Handler.CancelAsync(application.Id, Request));

            env.AsStudent(student);
            Assert.True((await env.Handler.CancelAsync(application.Id, Request)).IsSuccess);
            ResultAssert.Failed(await env.Handler.CancelAsync(application.Id, Request), GraduationErrors.InvalidState);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == GraduationAuditActions.Cancelled);
        }

        // ---------- the decision ----------

        [Fact]
        public async Task Approving_graduates_the_student_audits_with_the_approver_and_raises_the_event_in_one_save()
        {
            var (env, student, application) = await AppliedAsync();
            var savesBefore = env.Rbac.Uow.SaveCalls;
            env.AsRegistrar();

            var approved = ResultAssert.Succeeded(await env.Decisions.ApproveAsync(application.Id, "Congratulations", Request));

            Assert.Equal(GraduationApplicationStatus.Approved, approved.Status);
            Assert.Equal(3.0m, approved.CumulativeGpa);
            Assert.Equal(StudentAcademicStatus.Graduated, student.AcademicStatus);
            Assert.Equal(env.AcademicClock.Today, student.GraduationDate);
            Assert.Equal(savesBefore + 1, env.Rbac.Uow.SaveCalls);
            var stored = env.Applications.Items.Single();
            Assert.Single(stored.DomainEvents.OfType<GraduationApproved>());
            var audit = Assert.Single(env.Rbac.Audit.Entries, e => e.Action == GraduationAuditActions.Approved);
            Assert.NotNull(audit.ActorUserId);
        }

        [Fact]
        public async Task A_financial_hold_on_graduation_stops_the_approval()
        {
            var (env, student, application) = await AppliedAsync();
            env.Finance.Hold = true;
            var savesBefore = env.Rbac.Uow.SaveCalls;
            env.AsRegistrar();

            var result = await env.Decisions.ApproveAsync(application.Id, null, Request);

            ResultAssert.Failed(result, FinanceErrors.FinancialHold);
            Assert.Equal(StudentAcademicStatus.Active, student.AcademicStatus);
            Assert.Equal(GraduationApplicationStatus.Submitted, env.Applications.Items.Single().Status);
            Assert.Equal(savesBefore, env.Rbac.Uow.SaveCalls);

            // Released: the same application is approved.
            env.Finance.Hold = false;
            Assert.True((await env.Decisions.ApproveAsync(application.Id, null, Request)).IsSuccess);
        }

        [Fact]
        public async Task Eligibility_is_checked_again_at_the_decision()
        {
            var (env, student, application) = await AppliedAsync();

            // After applying, a grade correction lowers the GPA below the minimum.
            env.Gpa.Figures = new GpaFigures(1.8m, 6, 6, AcademicStanding.Warning);
            env.AsRegistrar();

            var result = await env.Decisions.ApproveAsync(application.Id, null, Request);

            Assert.True(result.IsFailure);
            Assert.Contains(GraduationErrors.GpaBelowMinimum.Code, result.Errors.Select(e => e.Code));
            Assert.Equal(StudentAcademicStatus.Active, student.AcademicStatus);
            Assert.Equal(GraduationApplicationStatus.Submitted, env.Applications.Items.Single().Status);
        }

        [Fact]
        public async Task Only_the_registrar_with_a_whole_university_scope_decides()
        {
            var (env, student, application) = await AppliedAsync();

            env.AsStudent(student);
            AssertForbidden(await env.Decisions.ApproveAsync(application.Id, null, Request));
            AssertForbidden(await env.Decisions.RejectAsync(application.Id, "no", Request));

            // The permission held only for a department is not enough: graduation is a university-wide decision.
            env.AsHead();
            AssertForbidden(await env.Decisions.ApproveAsync(application.Id, null, Request));
            AssertForbidden(await env.Decisions.RejectAsync(application.Id, "no", Request));

            Assert.Equal(GraduationApplicationStatus.Submitted, env.Applications.Items.Single().Status);
            Assert.Equal(StudentAcademicStatus.Active, student.AcademicStatus);
        }

        [Fact]
        public async Task An_application_is_decided_once()
        {
            var (env, _, application) = await AppliedAsync();
            env.AsRegistrar();

            Assert.True((await env.Decisions.ApproveAsync(application.Id, null, Request)).IsSuccess);

            ResultAssert.Failed(await env.Decisions.ApproveAsync(application.Id, null, Request), GraduationErrors.InvalidState);
            ResultAssert.Failed(await env.Decisions.RejectAsync(application.Id, "late", Request), GraduationErrors.InvalidState);
            Assert.Single(env.Applications.Items.Single().DomainEvents.OfType<GraduationApproved>());
        }

        [Fact]
        public async Task Rejecting_needs_a_reason_leaves_the_student_active_and_lets_them_apply_again()
        {
            var (env, student, application) = await AppliedAsync();
            env.AsRegistrar();

            ResultAssert.Failed(await env.Decisions.RejectAsync(application.Id, " ", Request), GraduationErrors.ReasonRequired);

            var rejected = ResultAssert.Succeeded(await env.Decisions.RejectAsync(application.Id, "Thesis not submitted", Request));
            Assert.Equal(GraduationApplicationStatus.Rejected, rejected.Status);
            Assert.Equal(StudentAcademicStatus.Active, student.AcademicStatus);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == GraduationAuditActions.Rejected);

            env.AsStudent(student);
            Assert.True((await env.Handler.ApplyAsync(student.Id, Request)).IsSuccess);
        }

        [Fact]
        public async Task A_graduated_student_cannot_apply_again()
        {
            var (env, student, application) = await AppliedAsync();
            env.AsRegistrar();
            Assert.True((await env.Decisions.ApproveAsync(application.Id, null, Request)).IsSuccess);

            env.AsStudent(student);
            // The approved application blocks a new one even before the status check.
            ResultAssert.Failed(await env.Handler.ApplyAsync(student.Id, Request), GraduationErrors.AlreadyApplied);
        }
    }
}
