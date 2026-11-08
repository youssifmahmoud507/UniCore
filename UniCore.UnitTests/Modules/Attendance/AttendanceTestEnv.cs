using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Attendance;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Documents;
using UniCore.Application.Modules.Offerings;
using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Attendance;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Documents;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.Offerings;
using UniCore.UnitTests.Modules.Scheduling;

namespace UniCore.UnitTests.Modules.Attendance
{
    // The roster of each section (the Registrations contract).
    internal sealed class FakeRoster : IEnrollmentReadService
    {
        public Dictionary<Guid, List<SectionRosterEntry>> Entries { get; } = [];

        public Task<Result<IReadOnlyList<EnrollmentInfo>>> GetStudentEnrollmentsAsync(Guid studentId, Guid semesterId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<EnrollmentInfo>>.Ok([]));

        public Task<Result<IReadOnlyList<SectionRosterEntry>>> GetRosterAsync(Guid courseSectionId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SectionRosterEntry> list = Entries.TryGetValue(courseSectionId, out var entries) ? [.. entries] : [];

            return Task.FromResult(Result<IReadOnlyList<SectionRosterEntry>>.Ok(list));
        }
    }

    // Stores nothing; counts the uploads and answers with a new document id.
    internal sealed class FakeDocuments : IDocumentService
    {
        public List<string?> UploadedTypes { get; } = [];

        public Task<Result<DocumentDto>> UploadAsync(string? typeCode, DocumentUpload upload, Guid? ownerUserId, Guid? actorUserId, CancellationToken cancellationToken = default)
        {
            UploadedTypes.Add(typeCode);
            var dto = new DocumentDto(Guid.NewGuid(), typeCode ?? string.Empty, false, upload.FileName ?? "file", upload.ContentType ?? "application/pdf", 10, 1, DocumentStatus.Active, ownerUserId, DateTimeOffset.UtcNow, null, []);

            return Task.FromResult(Result<DocumentDto>.Ok(dto));
        }

        public Task<Result<DocumentDto>> AddVersionAsync(Guid documentId, DocumentUpload upload, Guid? actorUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<DocumentDto>.Fail(DocumentErrors.NotFound));

        public Task<Result> VerifyLinkableAsync(Guid documentId, CancellationToken cancellationToken = default) => Task.FromResult(Result.Ok());

        public Task<Result<DocumentContent>> OpenAsync(Guid documentId, int? versionNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<DocumentContent>.Fail(DocumentErrors.NotFound));
    }

    // The real attendance handlers, evaluator and PermissionAuthorizer, wired to in-memory fakes.
    // Fall 2026: 1 Sep 2026 - 31 Jan 2027. Sessions are on Sundays of October.
    internal sealed class AttendanceTestEnv
    {
        public static readonly DateTimeOffset Start = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");
        public static readonly Guid DepartmentId = Guid.NewGuid();

        public FakeClock Clock { get; } = new(Start);
        public RbacTestEnv Rbac { get; } = new();

        public FakeAggregateRepository<AttendanceSession> Sessions { get; } = new();
        public FakeAggregateRepository<AttendancePolicy> Policies { get; } = new();
        public FakeAggregateRepository<AttendanceExcuse> Excuses { get; } = new();
        public FakeAggregateRepository<AttendanceWarning> Warnings { get; } = new();
        public FakeAggregateRepository<Student> Students { get; } = new();
        public FakeEntityRepository<AcademicProgram> Programs { get; } = new();
        public FakeEntityRepository<AcademicSemester> Semesters { get; } = new();
        public FakeSchedulingOfferings Offerings { get; } = new();
        public FakeCourseCatalog Catalog { get; } = new();
        public FakeRoster Roster { get; } = new();
        public FakeDocuments Documents { get; } = new();

        public AttendanceAccess Access { get; }
        public AttendanceEvaluator Evaluator { get; }
        public AttendancePolicyHandler PolicyHandler { get; }
        public AttendanceSessionHandler SessionHandler { get; }
        public AttendanceExcuseHandler ExcuseHandler { get; }
        public AttendanceReportHandler ReportHandler { get; }

        public AcademicSemester Semester { get; }

        public AttendanceTestEnv()
        {
            Access = new AttendanceAccess(Rbac.Authorizer, Offerings, Catalog, Semesters, Students, Programs);
            Evaluator = new AttendanceEvaluator(Offerings, Roster, Sessions, Policies, Warnings, Students, Rbac.Uow, Clock, Rbac.Audit, Rbac.User);
            PolicyHandler = new AttendancePolicyHandler(Access, Rbac.User, Policies, Rbac.Uow, Clock, Rbac.Audit);
            SessionHandler = new AttendanceSessionHandler(Access, Rbac.User, Sessions, Roster, Evaluator, Rbac.Uow, Clock, Rbac.Audit);
            ExcuseHandler = new AttendanceExcuseHandler(Access, Rbac.User, Sessions, Excuses, Documents, Evaluator, Rbac.Uow, Clock, Rbac.Audit);
            ReportHandler = new AttendanceReportHandler(Access, Evaluator, Warnings);

            Semester = SeedSemester();
        }

        // ---------- who is calling ----------

        // An instructor holds a Section grant for each section they teach.
        public void AsInstructor(params Guid[] sectionIds)
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Instructor, [Permissions.Attendance.Read, Permissions.Attendance.Record], [.. sectionIds.Select(id => new ScopeGrant(ScopeType.Section, id))]));

        public void AsStudent(Student student)
            => Rbac.SignIn(student.UserId!.Value, TestGrants.Role(RoleNames.Student, [Permissions.Attendance.Read, Permissions.Attendance.SubmitExcuse], new ScopeGrant(ScopeType.Department, Guid.NewGuid())));

        public void AsHead(Guid? departmentId = null)
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.DepartmentHead, [Permissions.Attendance.Read, Permissions.Attendance.ReviewExcuse, Permissions.Attendance.Manage], new ScopeGrant(ScopeType.Department, departmentId ?? DepartmentId)));

        // ---------- seed data ----------

        public AcademicSemester SeedSemester(bool closed = false)
        {
            var windows = new SemesterWindows(
                RegistrationStart: new(2026, 9, 1), RegistrationEnd: new(2026, 9, 10),
                AddDropStart: new(2026, 9, 5), AddDropEnd: new(2026, 9, 20),
                WithdrawalDeadline: new(2026, 11, 15),
                MidtermStart: new(2026, 11, 1), MidtermEnd: new(2026, 11, 10),
                FinalExamStart: new(2027, 1, 5), FinalExamEnd: new(2027, 1, 20),
                GradeSubmissionDeadline: new(2027, 1, 28), GradePublicationDate: new(2027, 2, 3));

            var semester = ResultAssert.Succeeded(AcademicSemester.Create(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 1), new DateOnly(2027, 1, 31), windows, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31), Clock.UtcNow));

            if (closed)
            {
                foreach (var status in new[] { SemesterStatus.RegistrationOpen, SemesterStatus.InProgress, SemesterStatus.Examinations, SemesterStatus.GradingOpen, SemesterStatus.Closed })
                {
                    ResultAssert.Succeeded(semester.AdvanceTo(status, Clock.UtcNow));
                }
            }

            Semesters.Items.Add(semester);
            return semester;
        }

        public SectionInfo SeedSection(AcademicSemester? semester = null)
        {
            var id = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var version = new CourseVersionInfo(Guid.NewGuid(), courseId, "CS101", DepartmentId, 1, "Intro", 3, 2, 2, CourseVersionStatus.Active, new DateOnly(2026, 1, 1), null);
            Catalog.Versions[version.CourseVersionId] = version;

            var info = new SectionInfo(id, Guid.NewGuid(), version.CourseVersionId, courseId, (semester ?? Semester).Id, "A", 30, CourseSectionStatus.Open, CourseOfferingStatus.Open, 3, true);
            Offerings.Sections[id] = info;
            Roster.Entries[id] = [];

            return info;
        }

        public Student SeedStudent(SectionInfo? enrolledIn = null)
        {
            var student = ResultAssert.Succeeded(Student.Create(Guid.NewGuid(), "2026CS" + Random.Shared.Next(1000, 9999), new DateOnly(2026, 8, 1), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Clock.UtcNow));
            ResultAssert.Succeeded(student.LinkAccount(Guid.NewGuid(), Clock.UtcNow));
            Students.Items.Add(student);

            if (enrolledIn is not null)
            {
                Roster.Entries[enrolledIn.SectionId].Add(new SectionRosterEntry(Guid.NewGuid(), student.Id, EnrollmentStatus.Registered));
            }

            return student;
        }

        public AttendancePolicy SeedCoursePolicy(SectionInfo section, decimal minimum = 75, decimal warning = 85, int lateMinutes = 15, bool blocking = true)
        {
            var policy = ResultAssert.Succeeded(AttendancePolicy.Create(null, section.CourseId, minimum, warning, lateMinutes, blocking, Clock.UtcNow));
            Policies.Items.Add(policy);
            return policy;
        }

        // ---------- calls ----------

        public async Task<SessionSummary> OpenOkAsync(SectionInfo section, int dayOfOctober = 4)
        {
            AsInstructor(section.SectionId);
            var result = await SessionHandler.OpenAsync(new OpenSessionCommand(section.SectionId, new DateOnly(2026, 10, dayOfOctober), new TimeOnly(9, 0)), Request);
            Assert.True(result.IsSuccess, result.Error?.Code);

            return result.Value!;
        }

        public async Task MarkOkAsync(SectionInfo section, SessionSummary session, params (Student Student, AttendanceStatus Status, int? Late)[] marks)
        {
            AsInstructor(section.SectionId);
            var result = await SessionHandler.MarkAsync(session.Id, new MarkAttendanceCommand([.. marks.Select(m => new StudentMark(m.Student.Id, m.Status, m.Late))]), Request);
            Assert.True(result.IsSuccess, result.Error?.Code);
        }

        public async Task CloseOkAsync(SectionInfo section, SessionSummary session)
        {
            AsInstructor(section.SectionId);
            var result = await SessionHandler.CloseAsync(session.Id, Request);
            Assert.True(result.IsSuccess, result.Error?.Code);
        }

        // One closed session where the given students are absent (Present for the rest of the roster).
        public async Task<SessionSummary> RunSessionAsync(SectionInfo section, int dayOfOctober, params Student[] absent)
        {
            var session = await OpenOkAsync(section, dayOfOctober);
            var marks = Roster.Entries[section.SectionId].Select(e => (Students.Items.First(s => s.Id == e.StudentId), absent.Any(a => a.Id == e.StudentId) ? AttendanceStatus.Absent : AttendanceStatus.Present, (int?)null)).ToArray();
            await MarkOkAsync(section, session, marks);
            await CloseOkAsync(section, session);

            return session;
        }
    }
}
