using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Grading;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Identity;
 
namespace UniCore.UnitTests.Modules.Grading
{
    public class GradingHandlerTests
    {
        private static readonly RequestInfo Request = GradingTestEnv.Request;

        private static void AssertFailed<T>(Result<T> result, Error expected)
        {
            Assert.True(result.IsFailure, "Expected " + expected.Code + " but it succeeded.");
            Assert.Equal(expected.Code, result.Error?.Code);
        }

        private static void AssertForbidden<T>(Result<T> result)
        {
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Forbidden, result.Error?.ErrorType);
        }

        // ---------- entering scores ----------

        [Fact]
        public async Task Only_the_assigned_instructor_enters_grades()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            var command = new EnterScoresCommand(section.SectionId, student.Id, [new ScoreInput(c.Quiz, 10)]);

            env.AsInstructor(Guid.NewGuid());
            AssertForbidden(await env.Entry.EnterScoresAsync(command, Request));

            env.AsHead();
            AssertForbidden(await env.Entry.EnterScoresAsync(command, Request));

            env.AsRegistrar();
            AssertForbidden(await env.Entry.EnterScoresAsync(command, Request));

            env.AsStudent(student);
            AssertForbidden(await env.Entry.EnterScoresAsync(command, Request));

            Assert.Empty(env.Grades.Items);

            env.AsInstructor(section.SectionId);
            Assert.True((await env.Entry.EnterScoresAsync(command, Request)).IsSuccess);
        }

        [Fact]
        public async Task Entering_creates_a_draft_updates_it_and_audits_with_old_and_new_values()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);

            var first = await env.EnterOkAsync(section, student, (c.Quiz, 10));
            Assert.Equal(GradeStatus.Draft, first.Status);
            Assert.Equal(3, first.Credits);

            var second = await env.EnterOkAsync(section, student, (c.Quiz, 15), (c.Midterm, 40));
            Assert.Equal(first.Id, second.Id);
            Assert.Equal(2, second.Scores.Count);
            Assert.Single(env.Grades.Items);
            Assert.Null(second.Percentage);

            var audits = env.Rbac.Audit.Entries.Where(e => e.Action == GradingAuditActions.ScoresEntered).ToList();
            Assert.Equal(2, audits.Count);
            Assert.Null(audits[0].OldValues);
            Assert.NotNull(audits[1].OldValues);
        }

        [Fact]
        public async Task Scores_must_belong_to_the_scheme_and_not_exceed_the_maximum()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);

            AssertFailed(await env.EnterAsync(section, student, (Guid.NewGuid(), 5)), GradingErrors.ComponentNotInScheme);
            AssertFailed(await env.EnterAsync(section, student, (c.Quiz, 21)), GradingErrors.ScoreInvalid);
            AssertFailed(await env.EnterAsync(section, student, (c.Quiz, -1)), GradingErrors.ScoreInvalid);
            Assert.Empty(env.Grades.Items);

            Assert.True((await env.EnterAsync(section, student, (c.Quiz, 20), (c.Final, 100))).IsSuccess);
        }

        [Fact]
        public async Task A_student_off_the_roster_has_no_grade()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var other = env.SeedStudent(env.SeedSection());
            var c = env.SeedScheme(section);

            AssertFailed(await env.EnterAsync(section, other, (c.Quiz, 10)), GradingErrors.StudentNotOnRoster);
        }

        [Fact]
        public async Task Entering_needs_a_published_scheme()
        {
            var env = new GradingTestEnv();
            var none = env.SeedSection();
            var draft = env.SeedSection();
            var studentA = env.SeedStudent(none);
            var studentB = env.SeedStudent(draft);
            var c = env.SeedScheme(draft, publish: false);

            AssertFailed(await env.EnterAsync(none, studentA, (Guid.NewGuid(), 1)), GradingErrors.AssessmentNotReady);
            AssertFailed(await env.EnterAsync(draft, studentB, (c.Quiz, 1)), GradingErrors.AssessmentNotReady);
        }

        [Fact]
        public async Task Entering_is_allowed_only_in_the_grade_submission_window_of_an_open_semester()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);

            env.Clock.Advance(TimeSpan.FromDays(7));
            AssertFailed(await env.EnterAsync(section, student, (c.Quiz, 10)), GradingErrors.WindowClosed);

            var closed = env.SeedSection(env.SeedSemester(closed: true));
            var closedStudent = env.SeedStudent(closed);
            var closedScheme = env.SeedScheme(closed);
            AssertFailed(await env.EnterAsync(closed, closedStudent, (closedScheme.Quiz, 10)), GradingErrors.SemesterClosed);
        }

        [Fact]
        public async Task Entering_needs_at_least_one_score()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            env.SeedScheme(section);
            env.AsInstructor(section.SectionId);

            AssertFailed(await env.Entry.EnterScoresAsync(new EnterScoresCommand(section.SectionId, student.Id, []), Request), GradingErrors.Invalid);
            AssertFailed(await env.Entry.EnterScoresAsync(new EnterScoresCommand(section.SectionId, student.Id, null), Request), GradingErrors.Invalid);
        }

        // ---------- submit ----------

        [Fact]
        public async Task Submit_calculates_percentage_letter_and_points()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var passing = env.SeedStudent(section);
            var failing = env.SeedStudent(section);
            var c = env.SeedScheme(section);

            // 18/20 x 20 = 18; 45/50 x 30 = 27; 90/100 x 50 = 45 -> 90.00 -> A-.
            await env.EnterAllOkAsync(section, passing, c, 18, 45, 90);
            // 20 + 6 + 10 = 36 -> F.
            await env.EnterAllOkAsync(section, failing, c, 20, 10, 20);

            await env.SubmitOkAsync(section);

            var a = env.Grades.Items.Single(g => g.StudentId == passing.Id);
            Assert.Equal(GradeStatus.Submitted, a.Status);
            Assert.Equal(90m, a.Percentage);
            Assert.Equal("A-", a.Letter);
            Assert.Equal(3.7m, a.GradePoints);
            Assert.True(a.IsPassing);

            var f = env.Grades.Items.Single(g => g.StudentId == failing.Id);
            Assert.Equal(36m, f.Percentage);
            Assert.Equal("F", f.Letter);
            Assert.Equal(0m, f.GradePoints);
            Assert.False(f.IsPassing);

            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == GradingAuditActions.Submitted && e.EntityName == "GradeSheet" && e.NewValues!.Contains("Count=2"));
        }

        [Fact]
        public async Task Submit_needs_a_grade_for_every_registered_student_with_every_component_scored()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var graded = env.SeedStudent(section);
            var missing = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, graded, c, 10, 20, 50);

            env.AsInstructor(section.SectionId);
            AssertFailed(await env.Entry.SubmitSectionAsync(section.SectionId, Request), GradingErrors.SectionIncomplete);

            await env.EnterOkAsync(section, missing, (c.Quiz, 10));
            env.AsInstructor(section.SectionId);
            AssertFailed(await env.Entry.SubmitSectionAsync(section.SectionId, Request), GradingErrors.ScoresIncomplete);
            Assert.All(env.Grades.Items, g => Assert.Equal(GradeStatus.Draft, g.Status));
        }

        [Fact]
        public async Task Submitting_twice_has_nothing_to_process()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, student, c, 10, 20, 50);
            await env.SubmitOkAsync(section);

            env.AsInstructor(section.SectionId);
            AssertFailed(await env.Entry.SubmitSectionAsync(section.SectionId, Request), GradingErrors.NothingToProcess);
        }

        // ---------- review, approve, publish, lock ----------

        [Fact]
        public async Task The_steps_are_done_by_their_own_role_on_the_department_of_the_course()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, student, c, 10, 20, 50);
            await env.SubmitOkAsync(section);

            env.AsInstructor(section.SectionId);
            AssertForbidden(await env.Workflow.ReviewSectionAsync(section.SectionId, Request));

            env.AsHead(Guid.NewGuid());
            AssertForbidden(await env.Workflow.ReviewSectionAsync(section.SectionId, Request));

            env.AsHead();
            AssertForbidden(await env.Workflow.ApproveSectionAsync(section.SectionId, Request));
            AssertForbidden(await env.Workflow.PublishSectionAsync(section.SectionId, Request));

            var reviewed = await env.Workflow.ReviewSectionAsync(section.SectionId, Request);
            Assert.True(reviewed.IsSuccess, reviewed.Error?.Code);
            Assert.Equal(GradeStatus.Reviewed, env.Grades.Items.Single().Status);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == GradingAuditActions.Reviewed);
        }

        [Fact]
        public async Task The_instructor_cannot_review_their_own_grades_even_when_the_role_allows_it()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);

            var me = Guid.NewGuid();
            env.AsUser(me, RoleNames.Instructor, [Permissions.Grades.Read, Permissions.Grades.Enter, Permissions.Grades.Review], new ScopeGrant(ScopeType.Section, section.SectionId), new ScopeGrant(ScopeType.Department, GradingTestEnv.DepartmentId));
            Assert.True((await env.Entry.EnterScoresAsync(new EnterScoresCommand(section.SectionId, student.Id, [new ScoreInput(c.Quiz, 10), new ScoreInput(c.Midterm, 20), new ScoreInput(c.Final, 50)]), Request)).IsSuccess);
            Assert.True((await env.Entry.SubmitSectionAsync(section.SectionId, Request)).IsSuccess);

            AssertFailed(await env.Workflow.ReviewSectionAsync(section.SectionId, Request), GradingErrors.SeparationOfDuties);
            Assert.Equal(GradeStatus.Submitted, env.Grades.Items.Single().Status);
        }

        [Fact]
        public async Task The_reviewer_cannot_approve_and_approving_needs_a_review_first()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, student, c, 10, 20, 50);
            await env.SubmitOkAsync(section);

            var dean = Guid.NewGuid();
            env.AsUser(dean, RoleNames.Dean, [Permissions.Grades.Read, Permissions.Grades.Review, Permissions.Grades.Approve], new ScopeGrant(ScopeType.Department, GradingTestEnv.DepartmentId));
            AssertFailed(await env.Workflow.ApproveSectionAsync(section.SectionId, Request), GradingErrors.NothingToProcess);

            Assert.True((await env.Workflow.ReviewSectionAsync(section.SectionId, Request)).IsSuccess);
            AssertFailed(await env.Workflow.ApproveSectionAsync(section.SectionId, Request), GradingErrors.SeparationOfDuties);

            env.AsDean();
            Assert.True((await env.Workflow.ApproveSectionAsync(section.SectionId, Request)).IsSuccess);
            Assert.Equal(GradeStatus.Approved, env.Grades.Items.Single().Status);
        }

        [Fact]
        public async Task Publishing_waits_for_the_publication_date_then_tells_gpa_and_notifications()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, student, c, 18, 45, 90);
            await env.ApproveOkAsync(section);

            env.AsRegistrar();
            AssertFailed(await env.Workflow.PublishSectionAsync(section.SectionId, Request), GradingErrors.PublicationDateNotReached);

            env.Clock.Advance(TimeSpan.FromDays(12));
            var published = await env.Workflow.PublishSectionAsync(section.SectionId, Request);

            Assert.True(published.IsSuccess, published.Error?.Code);
            Assert.Equal(1, published.Value!.Count);
            var grade = env.Grades.Items.Single();
            Assert.Equal(GradeStatus.Published, grade.Status);
            Assert.Contains(grade.DomainEvents, e => e is GradePublished { Letter: "A-", Credits: 3 });
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == GradingAuditActions.Published);
        }

        [Fact]
        public async Task A_locked_grade_cannot_be_changed_or_returned()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, student, c, 18, 45, 90);
            await env.PublishOkAsync(section);

            env.AsInstructor(section.SectionId);
            AssertFailed(await env.EnterAsync(section, student, (c.Quiz, 1)), GradingErrors.Locked);

            env.AsRegistrar();
            Assert.True((await env.Workflow.LockSectionAsync(section.SectionId, Request)).IsSuccess);
            Assert.Equal(GradeStatus.Locked, env.Grades.Items.Single().Status);

            AssertFailed(await env.EnterAsync(section, student, (c.Quiz, 1)), GradingErrors.Locked);

            env.AsHead();
            AssertFailed(await env.Workflow.ReturnSectionAsync(section.SectionId, "oops", Request), GradingErrors.NothingToProcess);
            Assert.Equal(18, env.Grades.Items.Single().Scores.Single(s => s.AssessmentComponentId == c.Quiz).Score);
        }

        [Fact]
        public async Task A_returned_section_goes_back_to_the_instructor_who_corrects_and_submits_again()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, student, c, 10, 20, 50);
            await env.SubmitOkAsync(section);

            env.AsHead();
            AssertFailed(await env.Workflow.ReturnSectionAsync(section.SectionId, " ", Request), GradingErrors.ReasonRequired);
            Assert.True((await env.Workflow.ReturnSectionAsync(section.SectionId, "Midterm mark looks wrong", Request)).IsSuccess);
            Assert.Equal(GradeStatus.Draft, env.Grades.Items.Single().Status);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == GradingAuditActions.Returned);

            await env.EnterOkAsync(section, student, (c.Midterm, 30));
            await env.SubmitOkAsync(section);
            Assert.Equal(GradeStatus.Submitted, env.Grades.Items.Single().Status);
        }

        // ---------- reading ----------

        [Fact]
        public async Task A_student_reads_only_their_own_published_grades()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var me = env.SeedStudent(section);
            var other = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            var mine = await env.EnterAllOkAsync(section, me, c, 18, 45, 90);
            await env.EnterAllOkAsync(section, other, c, 10, 20, 50);

            // Not published yet: not found to the student.
            env.AsStudent(me);
            AssertFailed(await env.Query.GetAsync(mine.Id), GradingErrors.NotFound);
            Assert.Empty((await env.Query.ListStudentAsync(me.Id, null)).Value!);

            await env.PublishOkAsync(section);

            env.AsStudent(me);
            Assert.True((await env.Query.GetAsync(mine.Id)).IsSuccess);
            Assert.Single((await env.Query.ListStudentAsync(me.Id, null)).Value!);

            // Someone else's grade, even published, is forbidden.
            var otherGrade = env.Grades.Items.Single(g => g.StudentId == other.Id);
            AssertForbidden(await env.Query.GetAsync(otherGrade.Id));
            AssertForbidden(await env.Query.ListStudentAsync(other.Id, null));
        }

        [Fact]
        public async Task Staff_read_the_sheet_of_their_own_sections_only()
        {
            var env = new GradingTestEnv();
            var mine = env.SeedSection();
            var theirs = env.SeedSection();
            var student = env.SeedStudent(mine);
            var c = env.SeedScheme(mine);
            await env.EnterAllOkAsync(mine, student, c, 10, 20, 50);

            env.AsInstructor(mine.SectionId);
            Assert.Single((await env.Query.ListSectionAsync(mine.SectionId, new(1, 10), null)).Value!.Items);
            AssertForbidden(await env.Query.ListSectionAsync(theirs.SectionId, new(1, 10), null));

            env.AsHead();
            Assert.True((await env.Query.ListSectionAsync(mine.SectionId, new(1, 10), GradeStatus.Draft)).IsSuccess);

            env.AsHead(Guid.NewGuid());
            AssertForbidden(await env.Query.ListSectionAsync(mine.SectionId, new(1, 10), null));
        }

        [Fact]
        public async Task The_default_scale_is_readable()
        {
            var env = new GradingTestEnv();
            env.AsRegistrar();

            var scale = await env.Query.GetDefaultScaleAsync();

            Assert.True(scale.IsSuccess, scale.Error?.Code);
            Assert.Equal(12, scale.Value!.Bands.Count);
            Assert.Equal("A+", scale.Value.Bands[0].Letter);
        }

        // ---------- contracts for GPA (Week 15) and grade changes (Developer 2) ----------

        [Fact]
        public async Task Read_service_returns_only_published_and_locked_grades()
        {
            var env = new GradingTestEnv();
            var published = env.SeedSection();
            var draft = env.SeedSection();
            var student = env.SeedStudent(published);
            env.Roster.Entries[draft.SectionId].Add(new UniCore.Application.Modules.Registrations.SectionRosterEntry(Guid.NewGuid(), student.Id, UniCore.Domain.Modules.Registrations.EnrollmentStatus.Registered));
            var c1 = env.SeedScheme(published);
            var c2 = env.SeedScheme(draft);
            await env.EnterAllOkAsync(published, student, c1, 18, 45, 90);
            await env.EnterOkAsync(draft, student, (c2.Quiz, 5));

            Assert.Empty((await env.Reader.GetPublishedGradesAsync(student.Id)).Value!);

            await env.PublishOkAsync(published);

            var records = (await env.Reader.GetPublishedGradesAsync(student.Id)).Value!;
            var record = Assert.Single(records);
            Assert.Equal("A-", record.Letter);
            Assert.Equal(3.7m, record.GradePoints);
            Assert.Equal(3, record.Credits);
        }

        [Fact]
        public async Task An_approved_correction_changes_a_final_grade_recalculates_and_names_the_approver()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, student, c, 18, 45, 90);
            await env.PublishOkAsync(section);
            var grade = env.Grades.Items.Single();
            var approver = Guid.NewGuid();

            // The final goes from 90 to 100: 18 + 27 + 50 = 95 -> A.
            var corrected = await env.Correction.ApplyApprovedCorrectionAsync(grade.Id, c.Final, 100, "Final paper was re-marked", approver, Request);

            Assert.True(corrected.IsSuccess, corrected.Error?.Code);
            Assert.Equal(95m, corrected.Value!.Percentage);
            Assert.Equal("A", corrected.Value.Letter);
            Assert.Equal(GradeStatus.Published, corrected.Value.Status);
            Assert.Contains(grade.DomainEvents, e => e is GradeCorrected { OldLetter: "A-", NewLetter: "A" });
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == GradingAuditActions.Corrected && e.ActorUserId == approver);
        }

        [Fact]
        public async Task A_correction_is_refused_for_a_grade_that_is_not_final_or_with_bad_input()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            var draft = await env.EnterAllOkAsync(section, student, c, 18, 45, 90);
            var approver = Guid.NewGuid();

            AssertFailed(await env.Correction.ApplyApprovedCorrectionAsync(draft.Id, c.Final, 100, "x", approver, Request), GradingErrors.InvalidState);
            AssertFailed(await env.Correction.ApplyApprovedCorrectionAsync(Guid.NewGuid(), c.Final, 100, "x", approver, Request), GradingErrors.NotFound);

            await env.PublishOkAsync(section);
            AssertFailed(await env.Correction.ApplyApprovedCorrectionAsync(draft.Id, Guid.NewGuid(), 100, "x", approver, Request), GradingErrors.CorrectionInvalid);
            AssertFailed(await env.Correction.ApplyApprovedCorrectionAsync(draft.Id, c.Final, 101, "x", approver, Request), GradingErrors.ScoreInvalid);
            AssertFailed(await env.Correction.ApplyApprovedCorrectionAsync(draft.Id, c.Final, 100, " ", approver, Request), GradingErrors.CorrectionInvalid);
        }
    }
}
