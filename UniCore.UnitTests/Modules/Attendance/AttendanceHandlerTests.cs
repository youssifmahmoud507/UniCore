using System.Text;
using UniCore.Application.Modules.Attendance;
using UniCore.Application.Modules.Documents;
using UniCore.Application.Modules.Offerings;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Attendance;
using UniCore.Domain.Modules.Students;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Attendance
{
    public class AttendanceHandlerTests
    {
        private static readonly RequestInfo Request = AttendanceTestEnv.Request;

        // ---------- policies ----------

        [Fact]
        public async Task Policy_is_managed_by_the_department_and_there_is_one_active_per_course()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            env.AsHead();

            var command = new CreatePolicyCommand(null, section.CourseId, 75, 85, 15, true);
            var created = await env.PolicyHandler.CreateAsync(command, Request);
            Assert.True(created.IsSuccess, created.Error?.Code);

            Assert.Equal("ATTENDANCE_POLICY_EXISTS", (await env.PolicyHandler.CreateAsync(command, Request)).Error?.Code);

            // After a deactivation a new policy may be created.
            Assert.True((await env.PolicyHandler.DeactivateAsync(created.Value!.Id, Request)).IsSuccess);
            Assert.True((await env.PolicyHandler.CreateAsync(command, Request)).IsSuccess);
        }

        [Fact]
        public async Task Policy_changes_need_the_permission_in_the_right_department()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var command = new CreatePolicyCommand(null, section.CourseId, 75, 85, 15, true);

            env.AsInstructor(section.SectionId);
            Assert.Equal(ErrorType.Forbidden, (await env.PolicyHandler.CreateAsync(command, Request)).Error?.ErrorType);

            env.AsHead(Guid.NewGuid());
            Assert.Equal(ErrorType.Forbidden, (await env.PolicyHandler.CreateAsync(command, Request)).Error?.ErrorType);

            env.AsHead();
            var created = await env.PolicyHandler.CreateAsync(command, Request);
            Assert.True(created.IsSuccess, created.Error?.Code);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == AttendanceAuditActions.PolicyCreated);
        }

        [Fact]
        public async Task A_policy_for_a_missing_course_is_refused()
        {
            var env = new AttendanceTestEnv();
            env.AsHead();

            var result = await env.PolicyHandler.CreateAsync(new CreatePolicyCommand(null, Guid.NewGuid(), 75, 85, 15, true), Request);

            Assert.Equal("ATTENDANCE_POLICY_TARGET_NOT_FOUND", result.Error?.Code);
        }

        // ---------- sessions ----------

        [Fact]
        public async Task An_instructor_opens_a_session_on_their_own_section_only()
        {
            var env = new AttendanceTestEnv();
            var mine = env.SeedSection();
            var other = env.SeedSection();

            env.AsInstructor(mine.SectionId);
            var opened = await env.SessionHandler.OpenAsync(new OpenSessionCommand(mine.SectionId, new DateOnly(2026, 10, 4), new TimeOnly(9, 0)), Request);
            Assert.True(opened.IsSuccess, opened.Error?.Code);
            Assert.Equal(AttendanceSessionStatus.Open, opened.Value!.Status);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == AttendanceAuditActions.SessionOpened);

            var denied = await env.SessionHandler.OpenAsync(new OpenSessionCommand(other.SectionId, new DateOnly(2026, 10, 4), new TimeOnly(9, 0)), Request);
            Assert.Equal(ErrorType.Forbidden, denied.Error?.ErrorType);
        }

        [Fact]
        public async Task Only_the_record_permission_opens_a_session()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var student = env.SeedStudent(section);

            env.AsStudent(student);
            var denied = await env.SessionHandler.OpenAsync(new OpenSessionCommand(section.SectionId, new DateOnly(2026, 10, 4), new TimeOnly(9, 0)), Request);

            Assert.Equal(ErrorType.Forbidden, denied.Error?.ErrorType);
        }

        [Fact]
        public async Task A_session_date_must_be_inside_the_semester_and_unique_for_the_section()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            env.AsInstructor(section.SectionId);

            var outside = await env.SessionHandler.OpenAsync(new OpenSessionCommand(section.SectionId, new DateOnly(2026, 8, 1), new TimeOnly(9, 0)), Request);
            Assert.Equal("ATTENDANCE_SESSION_DATE_OUTSIDE_SEMESTER", outside.Error?.Code);

            await env.OpenOkAsync(section, 4);
            var duplicate = await env.SessionHandler.OpenAsync(new OpenSessionCommand(section.SectionId, new DateOnly(2026, 10, 4), new TimeOnly(9, 0)), Request);
            Assert.Equal("ATTENDANCE_SESSION_EXISTS", duplicate.Error?.Code);
        }

        [Fact]
        public async Task Nothing_changes_in_a_closed_semester()
        {
            var env = new AttendanceTestEnv();
            var closed = env.SeedSemester(closed: true);
            var section = env.SeedSection(closed);
            env.AsInstructor(section.SectionId);

            var result = await env.SessionHandler.OpenAsync(new OpenSessionCommand(section.SectionId, new DateOnly(2026, 10, 4), new TimeOnly(9, 0)), Request);

            Assert.Equal("ATTENDANCE_SEMESTER_CLOSED", result.Error?.Code);
        }

        // ---------- marking ----------

        [Fact]
        public async Task Marks_are_saved_for_students_on_the_roster_and_audited()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var ali = env.SeedStudent(section);
            var mona = env.SeedStudent(section);
            var session = await env.OpenOkAsync(section);

            env.AsInstructor(section.SectionId);
            var marked = await env.SessionHandler.MarkAsync(session.Id, new MarkAttendanceCommand([new StudentMark(ali.Id, AttendanceStatus.Present, null), new StudentMark(mona.Id, AttendanceStatus.Late, 10)]), Request);

            Assert.True(marked.IsSuccess, marked.Error?.Code);
            Assert.Equal(2, marked.Value!.Records.Count);
            Assert.Contains(marked.Value.Records, r => r.StudentId == mona.Id && r.Status == AttendanceStatus.Late && r.MinutesLate == 10);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == AttendanceAuditActions.AttendanceMarked);
        }

        [Fact]
        public async Task A_student_who_is_not_on_the_roster_cannot_be_marked_and_nothing_is_saved()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var enrolled = env.SeedStudent(section);
            var stranger = env.SeedStudent();
            var session = await env.OpenOkAsync(section);
            var saves = env.Rbac.Uow.SaveCalls;

            env.AsInstructor(section.SectionId);
            var result = await env.SessionHandler.MarkAsync(session.Id, new MarkAttendanceCommand([new StudentMark(enrolled.Id, AttendanceStatus.Present, null), new StudentMark(stranger.Id, AttendanceStatus.Present, null)]), Request);

            Assert.Equal("ATTENDANCE_STUDENT_NOT_ENROLLED", result.Error?.Code);
            Assert.Equal(saves, env.Rbac.Uow.SaveCalls);
        }

        [Fact]
        public async Task The_same_student_twice_or_no_marks_is_refused()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var ali = env.SeedStudent(section);
            var session = await env.OpenOkAsync(section);

            env.AsInstructor(section.SectionId);
            var twice = await env.SessionHandler.MarkAsync(session.Id, new MarkAttendanceCommand([new StudentMark(ali.Id, AttendanceStatus.Present, null), new StudentMark(ali.Id, AttendanceStatus.Absent, null)]), Request);
            var none = await env.SessionHandler.MarkAsync(session.Id, new MarkAttendanceCommand([]), Request);

            Assert.Equal("ATTENDANCE_MARKS_REQUIRED", twice.Error?.Code);
            Assert.Equal("ATTENDANCE_MARKS_REQUIRED", none.Error?.Code);
        }

        [Fact]
        public async Task Another_instructor_cannot_mark_the_session()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var ali = env.SeedStudent(section);
            var session = await env.OpenOkAsync(section);

            env.AsInstructor(env.SeedSection().SectionId);
            var denied = await env.SessionHandler.MarkAsync(session.Id, new MarkAttendanceCommand([new StudentMark(ali.Id, AttendanceStatus.Present, null)]), Request);

            Assert.Equal(ErrorType.Forbidden, denied.Error?.ErrorType);
        }

        [Fact]
        public async Task Closing_marks_the_unmarked_as_absent_and_locks_the_session()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var ali = env.SeedStudent(section);
            var mona = env.SeedStudent(section);
            var session = await env.OpenOkAsync(section);
            await env.MarkOkAsync(section, session, (ali, AttendanceStatus.Present, null));

            env.AsInstructor(section.SectionId);
            var closed = await env.SessionHandler.CloseAsync(session.Id, Request);

            Assert.True(closed.IsSuccess, closed.Error?.Code);
            Assert.Equal(AttendanceSessionStatus.Closed, closed.Value!.Status);
            Assert.Contains(closed.Value.Records, r => r.StudentId == mona.Id && r.Status == AttendanceStatus.Absent);

            var again = await env.SessionHandler.MarkAsync(session.Id, new MarkAttendanceCommand([new StudentMark(ali.Id, AttendanceStatus.Absent, null)]), Request);
            Assert.Equal("ATTENDANCE_SESSION_CLOSED", again.Error?.Code);
            Assert.Equal("ATTENDANCE_SESSION_NOT_OPEN", (await env.SessionHandler.CloseAsync(session.Id, Request)).Error?.Code);
        }

        // ---------- percentage, warnings and blocking ----------

        [Fact]
        public async Task The_percentage_counts_closed_sessions_only()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var ali = env.SeedStudent(section);
            await env.RunSessionAsync(section, 4, ali);
            await env.RunSessionAsync(section, 11);

            // An open session is not counted yet.
            var open = await env.OpenOkAsync(section, 18);
            await env.MarkOkAsync(section, open, (ali, AttendanceStatus.Absent, null));

            var summary = (await env.Evaluator.GetStudentAttendanceAsync(ali.Id, section.SectionId)).Value!;

            Assert.Equal(2, summary.ClosedSessions);
            Assert.Equal(1, summary.Attended);
            Assert.Equal(1, summary.Absent);
            Assert.Equal(50m, summary.Percentage);
        }

        [Fact]
        public async Task Falling_below_the_minimum_issues_a_blocking_warning_and_raises_the_event()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var ali = env.SeedStudent(section);
            env.SeedCoursePolicy(section, minimum: 75, warning: 85, blocking: true);

            await env.RunSessionAsync(section, 4, ali);
            await env.RunSessionAsync(section, 11, ali);
            await env.RunSessionAsync(section, 18);
            await env.RunSessionAsync(section, 25);

            // Issued once, after the first absence (0%); later sessions at the same level do not warn again.
            var warning = Assert.Single(env.Warnings.Items.Where(w => w.StudentId == ali.Id && w.Level == WarningLevel.Critical));
            Assert.True(warning.IsExamBlocking);
            Assert.Equal(0m, warning.Percentage);
            Assert.Contains(warning.DomainEvents, e => e is AttendanceWarningIssued);

            Assert.True((await env.Evaluator.IsBlockedFromExamAsync(ali.Id, section.SectionId)).Value);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == AttendanceAuditActions.WarningIssued);
        }

        [Fact]
        public async Task A_policy_that_does_not_block_exams_warns_but_does_not_block()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var ali = env.SeedStudent(section);
            env.SeedCoursePolicy(section, blocking: false);

            await env.RunSessionAsync(section, 4, ali);

            Assert.Contains(env.Warnings.Items, w => w.StudentId == ali.Id && w.Level == WarningLevel.Critical && !w.IsExamBlocking);
            Assert.False((await env.Evaluator.IsBlockedFromExamAsync(ali.Id, section.SectionId)).Value);
        }

        [Fact]
        public async Task A_level_is_warned_once_and_only_a_worse_level_warns_again()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var ali = env.SeedStudent(section);
            env.SeedCoursePolicy(section, minimum: 50, warning: 80, blocking: true);

            // 9 present sessions then 1 absence: 90%, no warning. A second absence: 9/11... keep going to reach the Warning level.
            for (var day = 1; day <= 4; day++)
            {
                await env.RunSessionAsync(section, day);
            }

            await env.RunSessionAsync(section, 5, ali);   // 4/5 = 80%: not below the warning percentage
            Assert.Empty(env.Warnings.Items);

            await env.RunSessionAsync(section, 6, ali);   // 4/6 = 66.67%: Warning level
            Assert.Single(env.Warnings.Items);
            Assert.Equal(WarningLevel.Warning, env.Warnings.Items[0].Level);

            await env.RunSessionAsync(section, 7);        // 5/7 = 71.43%: still Warning, no new warning
            Assert.Single(env.Warnings.Items);

            await env.RunSessionAsync(section, 8, ali);   // 5/8 = 62.5%: still Warning
            await env.RunSessionAsync(section, 9, ali);   // 5/9 = 55.56%: still Warning
            await env.RunSessionAsync(section, 10, ali);  // 5/10 = 50%: equal to the minimum is still Warning
            Assert.Single(env.Warnings.Items);

            await env.RunSessionAsync(section, 11, ali);  // 5/11 = 45.45%: Critical
            Assert.Equal(2, env.Warnings.Items.Count);
            Assert.Equal(WarningLevel.Critical, env.Warnings.Items[1].Level);
        }

        [Fact]
        public async Task Without_a_policy_the_percentage_is_computed_but_nothing_warns_or_blocks()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var ali = env.SeedStudent(section);

            await env.RunSessionAsync(section, 4, ali);

            var summary = (await env.Evaluator.GetStudentAttendanceAsync(ali.Id, section.SectionId)).Value!;
            Assert.Equal(0m, summary.Percentage);
            Assert.Null(summary.Level);
            Assert.False(summary.IsExamBlocked);
            Assert.Empty(env.Warnings.Items);
        }

        // ---------- excuses ----------

        private static async Task<(AttendanceTestEnv Env, SectionInfo Section, Student Ali, Student Mona, Guid RecordId)> AbsentStudentAsync()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var ali = env.SeedStudent(section);
            var mona = env.SeedStudent(section);
            env.SeedCoursePolicy(section, minimum: 50, warning: 80, blocking: true);
            await env.RunSessionAsync(section, 4, ali);

            var recordId = env.Sessions.Items.Single().Records.Single(r => r.StudentId == ali.Id).Id;

            return (env, section, ali, mona, recordId);
        }

        [Fact]
        public async Task A_student_submits_an_excuse_with_a_file_stored_as_an_attendance_excuse_document()
        {
            var (env, _, ali, _, recordId) = await AbsentStudentAsync();
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 proof"));

            env.AsStudent(ali);
            var result = await env.ExcuseHandler.SubmitAsync(new SubmitExcuseCommand(recordId, "Hospital visit", new DocumentUpload("proof.pdf", "application/pdf", content)), Request);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(ExcuseStatus.Pending, result.Value!.Status);
            Assert.NotNull(result.Value.DocumentId);
            Assert.Equal([DocumentTypeCodes.AttendanceExcuse], env.Documents.UploadedTypes);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == AttendanceAuditActions.ExcuseSubmitted);
        }

        [Fact]
        public async Task An_invalid_excuse_uploads_no_file()
        {
            var (env, _, ali, _, recordId) = await AbsentStudentAsync();
            await using var content = new MemoryStream([1, 2, 3]);

            env.AsStudent(ali);
            var result = await env.ExcuseHandler.SubmitAsync(new SubmitExcuseCommand(recordId, "no", new DocumentUpload("proof.pdf", "application/pdf", content)), Request);

            Assert.Equal("ATTENDANCE_EXCUSE_INVALID", result.Error?.Code);
            Assert.Empty(env.Documents.UploadedTypes);
        }

        [Fact]
        public async Task A_student_cannot_excuse_someone_elses_absence_or_a_present_record()
        {
            var (env, _, ali, mona, recordId) = await AbsentStudentAsync();

            env.AsStudent(mona);
            var other = await env.ExcuseHandler.SubmitAsync(new SubmitExcuseCommand(recordId, "Hospital visit", null), Request);
            Assert.Equal(ErrorType.Forbidden, other.Error?.ErrorType);

            // Mona was present in the only session.
            var presentRecord = env.Sessions.Items.Single().Records.Single(r => r.StudentId == mona.Id).Id;
            var present = await env.ExcuseHandler.SubmitAsync(new SubmitExcuseCommand(presentRecord, "Hospital visit", null), Request);
            Assert.Equal("ATTENDANCE_EXCUSE_NOT_ABSENCE", present.Error?.Code);

            env.AsStudent(ali);
            Assert.True((await env.ExcuseHandler.SubmitAsync(new SubmitExcuseCommand(recordId, "Hospital visit", null), Request)).IsSuccess);
        }

        [Fact]
        public async Task One_pending_excuse_per_absence_but_a_new_one_after_a_rejection()
        {
            var (env, _, ali, _, recordId) = await AbsentStudentAsync();

            env.AsStudent(ali);
            var first = await env.ExcuseHandler.SubmitAsync(new SubmitExcuseCommand(recordId, "Hospital visit", null), Request);
            Assert.True(first.IsSuccess, first.Error?.Code);
            Assert.Equal("ATTENDANCE_EXCUSE_EXISTS", (await env.ExcuseHandler.SubmitAsync(new SubmitExcuseCommand(recordId, "Hospital visit again", null), Request)).Error?.Code);

            env.AsHead();
            Assert.True((await env.ExcuseHandler.ReviewAsync(first.Value!.Id, new ReviewExcuseCommand(false, "No proof"), Request)).IsSuccess);

            env.AsStudent(ali);
            Assert.True((await env.ExcuseHandler.SubmitAsync(new SubmitExcuseCommand(recordId, "Hospital visit, report attached", null), Request)).IsSuccess);
        }

        [Fact]
        public async Task An_accepted_excuse_turns_the_absence_into_excused_and_raises_the_percentage()
        {
            var (env, section, ali, _, recordId) = await AbsentStudentAsync();
            env.AsStudent(ali);
            var excuse = (await env.ExcuseHandler.SubmitAsync(new SubmitExcuseCommand(recordId, "Hospital visit", null), Request)).Value!;
            Assert.Equal(0m, (await env.Evaluator.GetStudentAttendanceAsync(ali.Id, section.SectionId)).Value!.Percentage);

            env.AsHead();
            var reviewed = await env.ExcuseHandler.ReviewAsync(excuse.Id, new ReviewExcuseCommand(true, null), Request);

            Assert.True(reviewed.IsSuccess, reviewed.Error?.Code);
            Assert.Equal(ExcuseStatus.Accepted, reviewed.Value!.Status);
            Assert.Equal(AttendanceStatus.Excused, env.Sessions.Items.Single().Records.Single(r => r.StudentId == ali.Id).Status);
            Assert.Equal(100m, (await env.Evaluator.GetStudentAttendanceAsync(ali.Id, section.SectionId)).Value!.Percentage);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == AttendanceAuditActions.ExcuseAccepted);
        }

        [Fact]
        public async Task A_rejection_needs_a_note_and_a_reviewed_excuse_cannot_be_reviewed_again()
        {
            var (env, _, ali, _, recordId) = await AbsentStudentAsync();
            env.AsStudent(ali);
            var excuse = (await env.ExcuseHandler.SubmitAsync(new SubmitExcuseCommand(recordId, "Hospital visit", null), Request)).Value!;

            env.AsHead();
            Assert.Equal("ATTENDANCE_EXCUSE_NOTE_REQUIRED", (await env.ExcuseHandler.ReviewAsync(excuse.Id, new ReviewExcuseCommand(false, null), Request)).Error?.Code);
            Assert.True((await env.ExcuseHandler.ReviewAsync(excuse.Id, new ReviewExcuseCommand(false, "No proof"), Request)).IsSuccess);
            Assert.Equal("ATTENDANCE_EXCUSE_NOT_PENDING", (await env.ExcuseHandler.ReviewAsync(excuse.Id, new ReviewExcuseCommand(true, null), Request)).Error?.Code);
        }

        [Fact]
        public async Task Only_a_reviewer_of_the_department_reviews_excuses()
        {
            var (env, section, ali, _, recordId) = await AbsentStudentAsync();
            env.AsStudent(ali);
            var excuse = (await env.ExcuseHandler.SubmitAsync(new SubmitExcuseCommand(recordId, "Hospital visit", null), Request)).Value!;

            env.AsStudent(ali);
            Assert.Equal(ErrorType.Forbidden, (await env.ExcuseHandler.ReviewAsync(excuse.Id, new ReviewExcuseCommand(true, null), Request)).Error?.ErrorType);

            env.AsHead(Guid.NewGuid());
            Assert.Equal(ErrorType.Forbidden, (await env.ExcuseHandler.ReviewAsync(excuse.Id, new ReviewExcuseCommand(true, null), Request)).Error?.ErrorType);
        }

        // ---------- reading ----------

        [Fact]
        public async Task A_student_reads_their_own_attendance_only()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var ali = env.SeedStudent(section);
            var mona = env.SeedStudent(section);
            await env.RunSessionAsync(section, 4, ali);

            env.AsStudent(ali);
            var own = await env.ReportHandler.GetStudentAsync(ali.Id, section.SectionId);
            Assert.True(own.IsSuccess, own.Error?.Code);
            Assert.Equal(0m, own.Value!.Percentage);

            Assert.Equal(ErrorType.Forbidden, (await env.ReportHandler.GetStudentAsync(mona.Id, section.SectionId)).Error?.ErrorType);
        }

        [Fact]
        public async Task The_section_report_lists_every_student_lowest_first_for_the_instructor()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var ali = env.SeedStudent(section);
            var mona = env.SeedStudent(section);
            await env.RunSessionAsync(section, 4, ali);
            await env.RunSessionAsync(section, 11);

            env.AsInstructor(section.SectionId);
            var report = await env.ReportHandler.GetSectionAsync(section.SectionId);

            Assert.True(report.IsSuccess, report.Error?.Code);
            Assert.Equal([ali.Id, mona.Id], report.Value!.Select(r => r.StudentId));
            Assert.Equal([50m, 100m], report.Value!.Select(r => r.Percentage));

            env.AsInstructor(env.SeedSection().SectionId);
            Assert.Equal(ErrorType.Forbidden, (await env.ReportHandler.GetSectionAsync(section.SectionId)).Error?.ErrorType);
        }

        [Fact]
        public async Task The_program_policy_applies_when_the_course_has_none()
        {
            var env = new AttendanceTestEnv();
            var section = env.SeedSection();
            var ali = env.SeedStudent(section);
            var programId = ali.CurrentProgram!.AcademicProgramId;
            env.Policies.Items.Add(ResultAssert.Succeeded(AttendancePolicy.Create(programId, null, 75, 85, 15, true, env.Clock.UtcNow)));

            await env.RunSessionAsync(section, 4, ali);

            Assert.True((await env.Evaluator.IsBlockedFromExamAsync(ali.Id, section.SectionId)).Value);
        }
    }
}
