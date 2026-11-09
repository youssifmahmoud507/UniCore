using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Attendance;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Examination;
using UniCore.Application.Modules.Offerings;
using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Examination;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.People;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;
using UniCore.UnitTests.Modules.Attendance;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.Offerings;
using UniCore.UnitTests.Modules.Scheduling;
using UniCore.UnitTests.Modules.University;

namespace UniCore.UnitTests.Modules.Examination
{
    // Answers the attendance rule of Week 11 from a set of blocked students.
    internal sealed class FakeAttendanceRule : IAttendanceReadService
    {
        public HashSet<Guid> Blocked { get; } = [];

        public Task<Result<StudentAttendanceSummary>> GetStudentAttendanceAsync(Guid studentId, Guid courseSectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<StudentAttendanceSummary>.Fail(Error.NotFound("TEST", "not used")));

        public Task<Result<bool>> IsBlockedFromExamAsync(Guid studentId, Guid courseSectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Ok(Blocked.Contains(studentId)));
    }

    // The real examination handlers and PermissionAuthorizer, wired to in-memory fakes.
    // Fall 2026: 1 Sep 2026 - 31 Jan 2027, finals 5-20 Jan 2027, midterms 1-10 Nov 2026.
    internal sealed class ExaminationTestEnv
    {
        public static readonly DateTimeOffset Start = new(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");
        public static readonly Guid DepartmentId = Guid.NewGuid();
        public static readonly DateOnly Day1 = new(2027, 1, 10);

        public FakeClock Clock { get; } = new(Start);
        public RbacTestEnv Rbac { get; } = new();

        public FakeAggregateRepository<ExamSession> Sessions { get; } = new();
        public FakeAggregateRepository<Exam> Exams { get; } = new();
        public FakeEntityRepository<Room> Rooms { get; } = new();
        public FakeEntityRepository<Instructor> Instructors { get; } = new();
        public FakeEntityRepository<AcademicSemester> Semesters { get; } = new();
        public FakeSchedulingOfferings Offerings { get; } = new();
        public FakeCourseCatalog Catalog { get; } = new();
        public FakeRoster Roster { get; } = new();
        public FakeAttendanceRule Attendance { get; } = new();

        public ExaminationAccess Access { get; }
        public ExamSessionHandler SessionHandler { get; }
        public ExamHandler Handler { get; }

        public AcademicSemester Semester { get; }

        public ExaminationTestEnv()
        {
            Access = new ExaminationAccess(Rbac.Authorizer, Offerings, Catalog, Semesters);
            var conflicts = new ExamConflictChecker(Exams, Roster);
            var roster = new ExamRosterService(Roster, Attendance);

            SessionHandler = new ExamSessionHandler(Access, Rbac.User, Sessions, Rbac.Uow, Clock, Rbac.Audit);
            Handler = new ExamHandler(Access, Rbac.User, Exams, Sessions, Rooms, Instructors, conflicts, roster, Rbac.Uow, Clock, Rbac.Audit);

            Semester = SeedSemester();
        }

        // ---------- who is calling ----------

        public void AsRegistrar()
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Registrar, [Permissions.Exams.Read, Permissions.Exams.Manage], new ScopeGrant(ScopeType.Global, null)));

        public void AsHead(Guid? departmentId = null)
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.DepartmentHead, [Permissions.Exams.Read, Permissions.Exams.Manage], new ScopeGrant(ScopeType.Department, departmentId ?? DepartmentId)));

        // An instructor reads the exams of the sections they teach and changes nothing.
        public void AsInstructor(params Guid[] sectionIds)
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Instructor, [Permissions.Exams.Read], [.. sectionIds.Select(id => new ScopeGrant(ScopeType.Section, id))]));

        public void AsStudent()
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Student, [Permissions.Attendance.Read], new ScopeGrant(ScopeType.Global, null)));

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

        public SectionInfo SeedSection(AcademicSemester? semester = null, int students = 0)
        {
            var id = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var version = new CourseVersionInfo(Guid.NewGuid(), courseId, "CS101", DepartmentId, 1, "Intro", 3, 2, 2, CourseVersionStatus.Active, new DateOnly(2026, 1, 1), null);
            Catalog.Versions[version.CourseVersionId] = version;

            var info = new SectionInfo(id, Guid.NewGuid(), version.CourseVersionId, courseId, (semester ?? Semester).Id, "A", 30, CourseSectionStatus.Open, CourseOfferingStatus.Open, 3, true);
            Offerings.Sections[id] = info;
            Roster.Entries[id] = [];

            for (var i = 0; i < students; i++)
            {
                SeedStudent(info);
            }

            return info;
        }

        public Guid SeedStudent(SectionInfo section)
        {
            var studentId = Guid.NewGuid();
            Roster.Entries[section.SectionId].Add(new SectionRosterEntry(Guid.NewGuid(), studentId, EnrollmentStatus.Registered));

            return studentId;
        }

        public Room SeedRoom(int capacity = 40, bool active = true)
        {
            var room = ResultAssert.Succeeded(Room.Create(Guid.NewGuid(), "R" + Random.Shared.Next(100, 999), null, capacity, RoomType.Classroom));

            if (!active)
            {
                ResultAssert.Succeeded(room.Deactivate());
            }

            Rooms.Items.Add(room);
            return room;
        }

        public Instructor SeedInstructor(bool active = true)
        {
            var instructor = ResultAssert.Succeeded(Instructor.Create(Guid.NewGuid(), AcademicRank.Lecturer, null, Clock.UtcNow));

            if (!active)
            {
                ResultAssert.Succeeded(instructor.Deactivate(Clock.UtcNow));
            }

            Instructors.Items.Add(instructor);
            return instructor;
        }

        // ---------- calls ----------

        public async Task<ExamSessionSummary> CreateSessionOkAsync(ExamType type = ExamType.Final, string name = "Finals Fall 2026", DateOnly? start = null, DateOnly? end = null)
        {
            AsRegistrar();
            var defaultStart = type == ExamType.Final ? new DateOnly(2027, 1, 8) : new DateOnly(2026, 11, 2);
            var defaultEnd = type == ExamType.Final ? new DateOnly(2027, 1, 15) : new DateOnly(2026, 11, 9);
            var result = await SessionHandler.CreateAsync(new CreateExamSessionCommand(Semester.Id, name, type, start ?? defaultStart, end ?? defaultEnd), Request);
            Assert.True(result.IsSuccess, result.Error?.Code);

            return result.Value!;
        }

        public async Task<ExamSummary> CreateExamOkAsync(SectionInfo section, ExamSessionSummary session, int minutes = 120)
        {
            AsHead();
            var result = await Handler.CreateAsync(new CreateExamCommand(section.SectionId, session.Id, session.ExamType, 100, minutes), Request);
            Assert.True(result.IsSuccess, result.Error?.Code);

            return result.Value!;
        }

        public Task<Result<ExamSummary>> ScheduleAsync(ExamSummary exam, DateOnly date, int startHour = 9, int endHour = 11)
        {
            AsHead();
            return Handler.SetScheduleAsync(exam.Id, new SetExamScheduleCommand(date, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0)), Request);
        }

        public async Task AddRoomOkAsync(ExamSummary exam, Room room)
        {
            AsHead();
            var result = await Handler.AddRoomAsync(exam.Id, new AddExamRoomCommand(room.Id), Request);
            Assert.True(result.IsSuccess, result.Error?.Code);
        }

        public Task<Result<ExamSummary>> AddInvigilatorAsync(ExamSummary exam, Room room, Instructor instructor, InvigilatorRole role = InvigilatorRole.Chief)
        {
            AsHead();
            return Handler.AddInvigilatorAsync(exam.Id, new AddInvigilatorCommand(room.Id, instructor.Id, role), Request);
        }
    }
}
