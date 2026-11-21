using UniCore.Application.Modules.Assessments;
using UniCore.Domain.Modules.Assessments;
using UniCore.Domain.Modules.Grading;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Grading
{
    // N-33 (the scheme a grade was entered against), N-34 (withdrawn students), N-35 (closing a semester).
    public class GradingReviewFixesTests
    {
        private static readonly UniCore.Application.Common.Abstractions.RequestInfo Request = GradingTestEnv.Request;

        // A student's roster entry is removed: the enrollment is no longer Registered or Completed.
        private static void Withdraw(GradingTestEnv env, UniCore.Application.Modules.Offerings.SectionInfo section, UniCore.Domain.Modules.Students.Student student)
            => env.Roster.Entries[section.SectionId].RemoveAll(e => e.StudentId == student.Id);

        // ---------- N-34: withdrawn students ----------

        [Fact]
        public async Task A_student_who_withdrew_with_an_unfinished_grade_does_not_block_the_submission_of_the_sheet()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var stays = env.SeedStudent(section);
            var leaves = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, stays, c, 18, 45, 90);
            await env.EnterOkAsync(section, leaves, (c.Quiz, 10));
            Withdraw(env, section, leaves);

            env.AsInstructor(section.SectionId);
            var submitted = await env.Entry.SubmitSectionAsync(section.SectionId, Request);

            Assert.True(submitted.IsSuccess, submitted.Error?.Code);
            Assert.Equal(1, submitted.Value!.Count);
            Assert.Equal(GradeStatus.Submitted, env.Grades.Items.Single(g => g.StudentId == stays.Id).Status);
            Assert.Equal(GradeStatus.Draft, env.Grades.Items.Single(g => g.StudentId == leaves.Id).Status);
        }

        [Fact]
        public async Task Review_and_approval_skip_the_grade_of_a_student_who_withdrew_after_submission()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var stays = env.SeedStudent(section);
            var leaves = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, stays, c, 18, 45, 90);
            await env.EnterAllOkAsync(section, leaves, c, 10, 20, 50);
            await env.SubmitOkAsync(section);
            Withdraw(env, section, leaves);

            env.AsHead();
            var reviewed = await env.Workflow.ReviewSectionAsync(section.SectionId, Request);

            Assert.True(reviewed.IsSuccess, reviewed.Error?.Code);
            Assert.Equal(1, reviewed.Value!.Count);
            Assert.Equal(GradeStatus.Reviewed, env.Grades.Items.Single(g => g.StudentId == stays.Id).Status);
            Assert.Equal(GradeStatus.Submitted, env.Grades.Items.Single(g => g.StudentId == leaves.Id).Status);
        }

        [Fact]
        public async Task A_withdrawn_students_grade_is_never_published_so_it_never_reaches_gpa_or_the_transcript()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var stays = env.SeedStudent(section);
            var leaves = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, stays, c, 18, 45, 90);
            await env.EnterAllOkAsync(section, leaves, c, 18, 45, 90);
            await env.SubmitOkAsync(section);
            Withdraw(env, section, leaves);

            env.AsHead();
            Assert.True((await env.Workflow.ReviewSectionAsync(section.SectionId, Request)).IsSuccess);
            env.AsDean();
            Assert.True((await env.Workflow.ApproveSectionAsync(section.SectionId, Request)).IsSuccess);
            env.Clock.Advance(TimeSpan.FromDays(12));
            env.AsRegistrar();
            var published = await env.Workflow.PublishSectionAsync(section.SectionId, Request);

            Assert.True(published.IsSuccess, published.Error?.Code);
            Assert.Equal(1, published.Value!.Count);
            Assert.Single((await env.Reader.GetPublishedGradesAsync(stays.Id)).Value!);
            Assert.Empty((await env.Reader.GetPublishedGradesAsync(leaves.Id)).Value!);
        }

        // ---------- N-33: the scheme of a grade ----------

        [Fact]
        public async Task Submitting_the_sheet_closes_the_scheme_in_the_same_save()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, student, c, 18, 45, 90);
            Assert.Equal(AssessmentStatus.Published, env.Assessments.Items.Single().Status);

            await env.SubmitOkAsync(section);

            Assert.Equal(AssessmentStatus.Closed, env.Assessments.Items.Single().Status);
        }

        [Fact]
        public async Task A_grade_returned_to_the_instructor_can_be_corrected_and_submitted_again_after_the_scheme_was_closed()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, student, c, 18, 45, 90);
            await env.SubmitOkAsync(section);

            env.AsHead();
            Assert.True((await env.Workflow.ReturnSectionAsync(section.SectionId, "Final looks wrong", Request)).IsSuccess);

            var fixedGrade = await env.EnterOkAsync(section, student, (c.Final, 95));
            Assert.Equal(GradeStatus.Draft, fixedGrade.Status);

            await env.SubmitOkAsync(section);
            Assert.Equal(GradeStatus.Submitted, env.Grades.Items.Single().Status);
        }

        [Fact]
        public async Task A_new_grade_still_needs_a_published_scheme()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var first = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, first, c, 18, 45, 90);
            await env.SubmitOkAsync(section);

            // The scheme is Closed now; a student with no grade yet cannot get one against it.
            var late = env.SeedStudent(section);
            var result = await env.EnterAsync(section, late, (c.Quiz, 10));

            Assert.Equal(GradingErrors.AssessmentNotReady.Code, result.Error?.Code);
        }

        [Fact]
        public async Task A_correction_reads_the_scheme_stored_on_the_grade_not_the_live_scheme_of_the_section()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, student, c, 18, 45, 90);
            await env.PublishOkAsync(section);
            var grade = env.Grades.Items.Single();

            // Another scheme appears for the same section and is the one a lookup by section finds first.
            var other = ResultAssert.Succeeded(Assessment.Create(section.SectionId, env.Clock.UtcNow));
            ResultAssert.Succeeded(other.AddComponent("Everything", ComponentType.Final, 100, 100, new DateOnly(2027, 1, 10), env.Clock.UtcNow));
            ResultAssert.Succeeded(other.Publish(env.Clock.UtcNow));
            env.Assessments.Items.Insert(0, other);

            var corrected = await env.Correction.ApplyApprovedCorrectionAsync(grade.Id, c.Final, 100, "Re-marked", Guid.NewGuid(), Request);

            Assert.True(corrected.IsSuccess, corrected.Error?.Code);
            Assert.Equal(95m, corrected.Value!.Percentage);
        }

        // ---------- N-35: closing a semester ----------

        [Fact]
        public async Task A_semester_with_an_unpublished_grade_cannot_be_closed_and_can_once_it_is_published()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, student, c, 18, 45, 90);

            var blocked = await env.CloseGuard.CanCloseAsync(env.Semester.Id);
            Assert.Equal(GradingErrors.UnpublishedGrades.Code, blocked.Error?.Code);

            await env.PublishOkAsync(section);

            Assert.True((await env.CloseGuard.CanCloseAsync(env.Semester.Id)).IsSuccess);
        }

        [Fact]
        public async Task A_withdrawn_students_unpublished_grade_does_not_keep_the_semester_open()
        {
            var env = new GradingTestEnv();
            var section = env.SeedSection();
            var stays = env.SeedStudent(section);
            var leaves = env.SeedStudent(section);
            var c = env.SeedScheme(section);
            await env.EnterAllOkAsync(section, stays, c, 18, 45, 90);
            await env.EnterOkAsync(section, leaves, (c.Quiz, 10));
            Withdraw(env, section, leaves);
            await env.PublishOkAsync(section);

            Assert.True((await env.CloseGuard.CanCloseAsync(env.Semester.Id)).IsSuccess);
        }

        [Fact]
        public async Task A_semester_with_no_grades_can_be_closed()
        {
            var env = new GradingTestEnv();

            Assert.True((await env.CloseGuard.CanCloseAsync(env.Semester.Id)).IsSuccess);
        }
    }
}
