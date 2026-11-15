using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Grading;
using UniCore.Application.Modules.Transcripts;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.Transcripts;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.Offerings;
using UniCore.UnitTests.Modules.University;

namespace UniCore.UnitTests.Modules.Transcripts
{
    // The grades the real GradeReadService would return, set by the test.
    internal sealed class FakeGrades : IGradeReadService
    {
        public List<GradeRecordInfo> Records { get; } = [];

        public Task<Result<IReadOnlyList<GradeRecordInfo>>> GetPublishedGradesAsync(Guid studentId, Guid? semesterId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<GradeRecordInfo>>.Ok([.. Records]));
    }

    internal sealed class TranscriptsTestEnv
    {
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");
        public static readonly Guid DepartmentId = Guid.NewGuid();
        public static readonly DateTimeOffset Start = new(2027, 2, 3, 9, 0, 0, TimeSpan.Zero);

        public FakeClock Clock { get; } = new(Start);
        public RbacTestEnv Rbac { get; } = new();
        public FakeGrades Grades { get; } = new();
        public FakeCourseCatalog Catalog { get; } = new();
        public FakeAggregateRepository<Student> Students { get; } = new();
        public FakeEntityRepository<AcademicProgram> Programs { get; } = new();
        public FakeEntityRepository<AcademicSemester> Semesters { get; } = new();
        public FakeEntityRepository<AcademicYear> Years { get; } = new();
        public FakeEntityRepository<AcademicTerm> Terms { get; } = new();
        public FakeAggregateRepository<TranscriptSnapshot> Snapshots { get; } = new();

        public TranscriptQueryHandler Query { get; }
        public TranscriptIssueHandler Issue { get; }
        public TranscriptSnapshotService SnapshotService { get; }
        public GpaCalculationService Gpa { get; }

        public TranscriptsTestEnv()
        {
            var access = new TranscriptAccess(Rbac.Authorizer, Students, Programs);
            var history = new AcademicHistoryBuilder(Grades, Catalog, Semesters, Years, Terms);

            Query = new TranscriptQueryHandler(access, history, Snapshots, Clock);
            SnapshotService = new TranscriptSnapshotService(access, history, Snapshots, Rbac.Uow, Clock, Rbac.Audit);
            Issue = new TranscriptIssueHandler(access, SnapshotService, Rbac.User);
            Gpa = new GpaCalculationService(history);
        }

        public void AsUser(Guid userId, string role, string[] permissions, params ScopeGrant[] grants)
            => Rbac.SignIn(userId, TestGrants.Role(role, permissions, grants));

        public void AsStudent(Student student)
            => AsUser(student.UserId!.Value, RoleNames.Student, [Permissions.Transcripts.Read], new ScopeGrant(ScopeType.Department, Guid.NewGuid()));

        public void AsRegistrar()
            => AsUser(Guid.NewGuid(), RoleNames.Registrar, [Permissions.Transcripts.Read, Permissions.Transcripts.Issue], new ScopeGrant(ScopeType.Global, null));

        public void AsHead()
            => AsUser(Guid.NewGuid(), RoleNames.DepartmentHead, [Permissions.Transcripts.Read], new ScopeGrant(ScopeType.Department, Guid.NewGuid()));

        public Student SeedStudent()
        {
            var student = ResultAssert.Succeeded(Student.Create(Guid.NewGuid(), "2026CS" + Random.Shared.Next(1000, 9999), new DateOnly(2026, 8, 1), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Clock.UtcNow));
            ResultAssert.Succeeded(student.LinkAccount(Guid.NewGuid(), Clock.UtcNow));
            Students.Items.Add(student);

            return student;
        }

        public AcademicSemester SeedSemester(DateOnly start)
        {
            var windows = new SemesterWindows(
                RegistrationStart: start, RegistrationEnd: start.AddDays(9), AddDropStart: start.AddDays(4), AddDropEnd: start.AddDays(19),
                WithdrawalDeadline: start.AddDays(70), MidtermStart: start.AddDays(60), MidtermEnd: start.AddDays(69),
                FinalExamStart: start.AddDays(120), FinalExamEnd: start.AddDays(135), GradeSubmissionDeadline: start.AddDays(145), GradePublicationDate: start.AddDays(155));

            var semester = ResultAssert.Succeeded(AcademicSemester.Create(Guid.NewGuid(), Guid.NewGuid(), start, start.AddDays(150), windows, start, start.AddDays(300), Clock.UtcNow));
            Semesters.Items.Add(semester);

            return semester;
        }

        public Guid SeedCourse(string code)
        {
            var courseId = Guid.NewGuid();
            var version = new CourseVersionInfo(Guid.NewGuid(), courseId, code, DepartmentId, 1, "Title " + code, 3, 2, 2, CourseVersionStatus.Active, new DateOnly(2026, 1, 1), null);
            Catalog.Versions[version.CourseVersionId] = version;

            return courseId;
        }

        public GradeRecordInfo AddGrade(Guid courseId, Guid semesterId, int credits, decimal points, bool passing = true)
        {
            var record = new GradeRecordInfo(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), semesterId, courseId, credits, passing ? 85m : 40m, passing ? "B+" : "F", points, passing, GradeStatus.Published, Clock.UtcNow);
            Grades.Records.Add(record);

            return record;
        }
    }

    public class TranscriptsHandlerTests
    {
        private static void AssertForbidden<T>(Result<T> result)
        {
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Forbidden, result.Error?.ErrorType);
        }

        private static (TranscriptsTestEnv Env, Student Student) TwoSemesters()
        {
            var env = new TranscriptsTestEnv();
            var student = env.SeedStudent();
            var fall = env.SeedSemester(new DateOnly(2026, 9, 1));
            var spring = env.SeedSemester(new DateOnly(2027, 2, 15));

            env.AddGrade(env.SeedCourse("CS101"), fall.Id, 3, 4.0m);
            env.AddGrade(env.SeedCourse("CS102"), spring.Id, 3, 2.0m);

            return (env, student);
        }

        // ---------- who can read ----------

        [Fact]
        public async Task A_student_reads_their_own_transcript_but_not_another_students()
        {
            var (env, student) = TwoSemesters();
            var other = env.SeedStudent();

            env.AsStudent(student);
            Assert.True((await env.Query.GetTranscriptAsync(student.Id)).IsSuccess);
            AssertForbidden(await env.Query.GetTranscriptAsync(other.Id));
            AssertForbidden(await env.Query.GetGpaAsync(other.Id));
            AssertForbidden(await env.Query.GetHistoryAsync(other.Id));
        }

        [Fact]
        public async Task Without_the_permission_nothing_is_read_and_an_unknown_student_is_not_found_to_those_who_have_it()
        {
            var (env, student) = TwoSemesters();

            env.AsUser(Guid.NewGuid(), RoleNames.Instructor, []);
            AssertForbidden(await env.Query.GetTranscriptAsync(student.Id));

            env.AsRegistrar();
            var missing = await env.Query.GetTranscriptAsync(Guid.NewGuid());
            Assert.Equal(TranscriptErrors.StudentNotFound.Code, missing.Error?.Code);
        }

        [Fact]
        public async Task The_registrar_reads_every_students_gpa()
        {
            var (env, student) = TwoSemesters();
            env.AsRegistrar();

            var gpa = await env.Query.GetGpaAsync(student.Id);

            Assert.True(gpa.IsSuccess);
            Assert.Equal(3.00m, gpa.Value!.Cumulative.Gpa);
            Assert.Equal(6, gpa.Value.Cumulative.AttemptedCredits);
        }

        // ---------- the read models ----------

        [Fact]
        public async Task The_academic_history_lists_semesters_in_date_order_with_labels_and_courses()
        {
            var (env, student) = TwoSemesters();
            env.AsStudent(student);

            var history = (await env.Query.GetHistoryAsync(student.Id)).Value!;

            Assert.Equal(2, history.Semesters.Count);
            Assert.StartsWith("Semester starting 2026-09-01", history.Semesters[0].Label);
            Assert.Equal("CS101", history.Semesters[0].Courses.Single().CourseCode);
            Assert.Equal(4.00m, history.Semesters[0].Semester.Gpa);
            Assert.Equal(3.00m, history.Semesters[1].CumulativeAtEnd.Gpa);
            Assert.Equal(student.StudentNumber, history.StudentNumber);
        }

        [Fact]
        public async Task The_unofficial_transcript_has_no_verification_code_and_stores_nothing()
        {
            var (env, student) = TwoSemesters();
            env.AsStudent(student);

            var transcript = (await env.Query.GetTranscriptAsync(student.Id)).Value!;

            Assert.False(transcript.IsOfficial);
            Assert.Null(transcript.VerificationCode);
            Assert.Null(transcript.SnapshotId);
            Assert.Empty(env.Snapshots.Items);
        }

        [Fact]
        public async Task A_repeated_course_counts_once_and_the_earlier_attempt_is_shown_as_not_counted()
        {
            var env = new TranscriptsTestEnv();
            var student = env.SeedStudent();
            var fall = env.SeedSemester(new DateOnly(2026, 9, 1));
            var spring = env.SeedSemester(new DateOnly(2027, 2, 15));
            var course = env.SeedCourse("CS101");
            env.AddGrade(course, fall.Id, 3, 0m, passing: false);
            env.AddGrade(course, spring.Id, 3, 3.0m);
            env.AsStudent(student);

            var history = (await env.Query.GetHistoryAsync(student.Id)).Value!;

            Assert.Equal(3.00m, history.Cumulative.Gpa);
            Assert.Equal(3, history.Cumulative.AttemptedCredits);
            Assert.Equal(1, history.ReplacedAttempts);
            Assert.False(history.Semesters[0].Courses.Single().IsCounted);
        }

        [Fact]
        public async Task A_semester_gpa_without_grades_is_reported()
        {
            var (env, student) = TwoSemesters();
            env.AsRegistrar();

            var result = await env.Query.GetSemesterGpaAsync(student.Id, Guid.NewGuid());

            Assert.Equal(TranscriptErrors.NoGrades.Code, result.Error?.Code);
        }

        [Fact]
        public async Task A_grade_whose_course_cannot_be_found_fails_instead_of_guessing()
        {
            var env = new TranscriptsTestEnv();
            var student = env.SeedStudent();
            var semester = env.SeedSemester(new DateOnly(2026, 9, 1));
            env.AddGrade(Guid.NewGuid(), semester.Id, 3, 3.0m);
            env.AsStudent(student);

            var result = await env.Query.GetTranscriptAsync(student.Id);

            Assert.Equal(TranscriptErrors.CourseNotFound.Code, result.Error?.Code);
        }

        // ---------- the GPA contract ----------

        [Fact]
        public async Task The_gpa_service_calculates_without_any_permission_check()
        {
            var (env, student) = TwoSemesters();
            env.AsUser(Guid.NewGuid(), RoleNames.Instructor, []);

            var cumulative = await env.Gpa.GetCumulativeAsync(student.Id);

            Assert.True(cumulative.IsSuccess);
            Assert.Equal(3.00m, cumulative.Value!.Cumulative.Gpa);
            Assert.Equal(AcademicStanding.GoodStanding, cumulative.Value.Cumulative.Standing);
        }

        [Fact]
        public async Task A_student_without_grades_has_zero_credits_and_no_standing()
        {
            var env = new TranscriptsTestEnv();
            var student = env.SeedStudent();

            var cumulative = (await env.Gpa.GetCumulativeAsync(student.Id)).Value!;

            Assert.Equal(0, cumulative.Cumulative.AttemptedCredits);
            Assert.Null(cumulative.Cumulative.Standing);
        }

        // ---------- the official transcript ----------

        [Fact]
        public async Task Only_the_registrar_issues_an_official_transcript()
        {
            var (env, student) = TwoSemesters();

            env.AsStudent(student);
            AssertForbidden(await env.Issue.IssueAsync(student.Id, TranscriptsTestEnv.Request));

            env.AsHead();
            AssertForbidden(await env.Issue.IssueAsync(student.Id, TranscriptsTestEnv.Request));

            Assert.Empty(env.Snapshots.Items);
            Assert.Equal(0, env.Rbac.Uow.SaveCalls);
        }

        [Fact]
        public async Task Issuing_freezes_the_transcript_audits_it_and_a_later_correction_does_not_change_it()
        {
            var (env, student) = TwoSemesters();
            env.AsRegistrar();

            var issued = (await env.Issue.IssueAsync(student.Id, TranscriptsTestEnv.Request)).Value!;

            Assert.True(issued.IsOfficial);
            Assert.Equal(12, issued.VerificationCode!.Length);
            Assert.Equal(3.00m, issued.Cumulative.Gpa);
            Assert.Single(env.Snapshots.Items);
            Assert.Equal(1, env.Rbac.Uow.SaveCalls);
            var audit = Assert.Single(env.Rbac.Audit.Entries, e => e.Action == TranscriptAuditActions.Issued);
            Assert.Equal(issued.SnapshotId.ToString(), audit.EntityId);

            // A grade is corrected afterwards: the frozen transcript still shows the old one, a new calculation shows the new one.
            var fresh = env.Grades.Records[1] with { GradePoints = 4.0m };
            env.Grades.Records[1] = fresh;

            var frozen = (await env.Query.GetSnapshotAsync(issued.SnapshotId!.Value)).Value!;
            var live = (await env.Query.GetTranscriptAsync(student.Id)).Value!;

            Assert.Equal(3.00m, frozen.Cumulative.Gpa);
            Assert.Equal(4.00m, live.Cumulative.Gpa);
        }

        [Fact]
        public async Task A_student_with_no_published_grades_gets_no_official_transcript()
        {
            var env = new TranscriptsTestEnv();
            var student = env.SeedStudent();
            env.AsRegistrar();

            var result = await env.Issue.IssueAsync(student.Id, TranscriptsTestEnv.Request);

            Assert.Equal(TranscriptErrors.NoGrades.Code, result.Error?.Code);
            Assert.Empty(env.Snapshots.Items);
        }

        [Fact]
        public async Task The_workflow_service_issues_for_the_approver_without_a_permission_check()
        {
            var (env, student) = TwoSemesters();
            var approver = Guid.NewGuid();
            env.AsUser(Guid.NewGuid(), RoleNames.Instructor, []);

            var issued = await env.SnapshotService.IssueOfficialAsync(student.Id, approver, TranscriptsTestEnv.Request);

            Assert.True(issued.IsSuccess);
            Assert.Equal(approver, env.Snapshots.Items.Single().IssuedByUserId);
            Assert.Equal(approver, env.Rbac.Audit.Entries.Single(e => e.Action == TranscriptAuditActions.Issued).ActorUserId);
        }

        [Fact]
        public async Task A_student_reads_their_own_snapshots_and_the_list_shows_only_theirs()
        {
            var (env, student) = TwoSemesters();
            var other = env.SeedStudent();
            env.AddGrade(env.SeedCourse("CS201"), env.Semesters.Items[0].Id, 3, 3.0m);
            env.AsRegistrar();
            var mine = (await env.Issue.IssueAsync(student.Id, TranscriptsTestEnv.Request)).Value!;

            env.AsStudent(student);
            Assert.Single((await env.Query.ListSnapshotsAsync(student.Id)).Value!);
            Assert.True((await env.Query.GetSnapshotAsync(mine.SnapshotId!.Value)).IsSuccess);

            env.AsStudent(other);
            AssertForbidden(await env.Query.GetSnapshotAsync(mine.SnapshotId!.Value));
            AssertForbidden(await env.Query.ListSnapshotsAsync(student.Id));
        }

        [Fact]
        public async Task An_unknown_snapshot_is_not_found()
        {
            var (env, _) = TwoSemesters();
            env.AsRegistrar();

            var result = await env.Query.GetSnapshotAsync(Guid.NewGuid());

            Assert.Equal(TranscriptErrors.SnapshotNotFound.Code, result.Error?.Code);
        }
    }
}
