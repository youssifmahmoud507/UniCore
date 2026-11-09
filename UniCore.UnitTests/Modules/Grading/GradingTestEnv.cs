using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Assessments;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Grading;
using UniCore.Application.Modules.Offerings;
using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Modules.Assessments;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;
using UniCore.UnitTests.Modules.Attendance;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.Offerings;
using UniCore.UnitTests.Modules.Scheduling;
using UniCore.UnitTests.Modules.University;

namespace UniCore.UnitTests.Modules.Grading
{
    // The real grading handlers, engine and PermissionAuthorizer, wired to in-memory fakes.
    // Fall 2026: 1 Sep 2026 - 31 Jan 2027, grade submission deadline 28 Jan 2027, grade publication date 3 Feb 2027.
    // The clock starts on 22 Jan 2027: inside the grade submission window, before the publication date.
    internal sealed class GradingTestEnv
    {
        public static readonly DateTimeOffset Start = new(2027, 1, 22, 9, 0, 0, TimeSpan.Zero);
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");
        public static readonly Guid DepartmentId = Guid.NewGuid();

        public FakeClock Clock { get; } = new(Start);
        public RbacTestEnv Rbac { get; } = new();

        public FakeAggregateRepository<Grade> Grades { get; } = new();
        public FakeAggregateRepository<GradeScale> Scales { get; } = new();
        public FakeAggregateRepository<Assessment> Assessments { get; } = new();
        public FakeAggregateRepository<Student> Students { get; } = new();
        public FakeEntityRepository<AcademicProgram> Programs { get; } = new();
        public FakeEntityRepository<AcademicSemester> Semesters { get; } = new();
        public FakeSchedulingOfferings Offerings { get; } = new();
        public FakeCourseCatalog Catalog { get; } = new();
        public FakeRoster Roster { get; } = new();

        public GradingAccess Access { get; }
        public GradingEngine Engine { get; }
        public GradeEntryHandler Entry { get; }
        public GradeWorkflowHandler Workflow { get; }
        public GradeQueryHandler Query { get; }
        public GradeReadService Reader { get; }
        public GradeCorrectionService Correction { get; }

        public AcademicSemester Semester { get; }

        public GradingTestEnv()
        {
            Access = new GradingAccess(Rbac.Authorizer, Offerings, Catalog, Semesters, Students, Programs);
            Engine = new GradingEngine(new AssessmentReadService(Assessments), Scales);
            Entry = new GradeEntryHandler(Access, Engine, Rbac.User, Roster, Grades, Rbac.Uow, Clock, Rbac.Audit);
            Workflow = new GradeWorkflowHandler(Access, Rbac.User, Grades, Rbac.Uow, Clock, Rbac.Audit);
            Query = new GradeQueryHandler(Access, Engine, Rbac.User, Grades);
            Reader = new GradeReadService(Grades);
            Correction = new GradeCorrectionService(Engine, Grades, Rbac.Uow, Clock, Rbac.Audit);

            Scales.Items.Add(ResultAssert.Succeeded(GradeScale.Create(GradeScale.StandardName, [.. GradeScale.StandardBands], isDefault: true, Clock.UtcNow)));
            Semester = SeedSemester();
        }

        // ---------- who is calling ----------

        public void AsUser(Guid userId, string role, string[] permissions, params ScopeGrant[] grants)
            => Rbac.SignIn(userId, TestGrants.Role(role, permissions, grants));

        // An instructor holds a Section grant for each section they teach.
        public void AsInstructor(params Guid[] sectionIds)
            => AsUser(Guid.NewGuid(), RoleNames.Instructor, [Permissions.Grades.Read, Permissions.Grades.Enter], [.. sectionIds.Select(id => new ScopeGrant(ScopeType.Section, id))]);

        public void AsHead(Guid? departmentId = null)
            => AsUser(Guid.NewGuid(), RoleNames.DepartmentHead, [Permissions.Grades.Read, Permissions.Grades.Review], new ScopeGrant(ScopeType.Department, departmentId ?? DepartmentId));

        public void AsDean(Guid? departmentId = null)
            => AsUser(Guid.NewGuid(), RoleNames.Dean, [Permissions.Grades.Read, Permissions.Grades.Review, Permissions.Grades.Approve], new ScopeGrant(ScopeType.Department, departmentId ?? DepartmentId));

        public void AsRegistrar()
            => AsUser(Guid.NewGuid(), RoleNames.Registrar, [Permissions.Grades.Read, Permissions.Grades.Publish], new ScopeGrant(ScopeType.Global, null));

        public void AsStudent(Student student)
            => AsUser(student.UserId!.Value, RoleNames.Student, [Permissions.Grades.Read], new ScopeGrant(ScopeType.Department, Guid.NewGuid()));

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

        public Student SeedStudent(SectionInfo section)
        {
            var student = ResultAssert.Succeeded(Student.Create(Guid.NewGuid(), "2026CS" + Random.Shared.Next(1000, 9999), new DateOnly(2026, 8, 1), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Clock.UtcNow));
            ResultAssert.Succeeded(student.LinkAccount(Guid.NewGuid(), Clock.UtcNow));
            Students.Items.Add(student);
            Roster.Entries[section.SectionId].Add(new SectionRosterEntry(Guid.NewGuid(), student.Id, EnrollmentStatus.Registered));

            return student;
        }

        // Quizzes 20% (max 20), Midterm 30% (max 50), Final 50% (max 100). Published unless `publish` is false.
        public (Guid Quiz, Guid Midterm, Guid Final) SeedScheme(SectionInfo section, bool publish = true)
        {
            var due = new DateOnly(2026, 12, 1);
            var assessment = ResultAssert.Succeeded(Assessment.Create(section.SectionId, Clock.UtcNow));
            var quiz = ResultAssert.Succeeded(assessment.AddComponent("Quizzes", ComponentType.Quiz, 20, 20, due, Clock.UtcNow));
            var midterm = ResultAssert.Succeeded(assessment.AddComponent("Midterm", ComponentType.Midterm, 30, 50, new DateOnly(2026, 11, 5), Clock.UtcNow));
            var final = ResultAssert.Succeeded(assessment.AddComponent("Final", ComponentType.Final, 50, 100, new DateOnly(2027, 1, 10), Clock.UtcNow));

            if (publish)
            {
                ResultAssert.Succeeded(assessment.Publish(Clock.UtcNow));
            }

            Assessments.Items.Add(assessment);
            return (quiz.Id, midterm.Id, final.Id);
        }

        // ---------- calls ----------

        public Task<Domain.Common.Results.Result<GradeSummary>> EnterAsync(SectionInfo section, Student student, params (Guid ComponentId, decimal Score)[] scores)
        {
            AsInstructor(section.SectionId);

            return Entry.EnterScoresAsync(new EnterScoresCommand(section.SectionId, student.Id, [.. scores.Select(s => new ScoreInput(s.ComponentId, s.Score))]), Request);
        }

        public async Task<GradeSummary> EnterOkAsync(SectionInfo section, Student student, params (Guid ComponentId, decimal Score)[] scores)
        {
            var result = await EnterAsync(section, student, scores);
            Assert.True(result.IsSuccess, result.Error?.Code);

            return result.Value!;
        }

        // Scores for all three components: quiz, midterm, final.
        public Task<GradeSummary> EnterAllOkAsync(SectionInfo section, Student student, (Guid Quiz, Guid Midterm, Guid Final) c, decimal quiz, decimal midterm, decimal final)
            => EnterOkAsync(section, student, (c.Quiz, quiz), (c.Midterm, midterm), (c.Final, final));

        public async Task SubmitOkAsync(SectionInfo section)
        {
            AsInstructor(section.SectionId);
            var result = await Entry.SubmitSectionAsync(section.SectionId, Request);
            Assert.True(result.IsSuccess, result.Error?.Code);
        }

        // Draft -> Submitted -> Reviewed -> Approved by three different people.
        public async Task ApproveOkAsync(SectionInfo section)
        {
            await SubmitOkAsync(section);
            AsHead();
            Assert.True((await Workflow.ReviewSectionAsync(section.SectionId, Request)).IsSuccess);
            AsDean();
            var approved = await Workflow.ApproveSectionAsync(section.SectionId, Request);
            Assert.True(approved.IsSuccess, approved.Error?.Code);
        }

        // ... and Published by the registrar on the publication date.
        public async Task PublishOkAsync(SectionInfo section)
        {
            await ApproveOkAsync(section);
            Clock.Advance(TimeSpan.FromDays(12));
            AsRegistrar();
            var published = await Workflow.PublishSectionAsync(section.SectionId, Request);
            Assert.True(published.IsSuccess, published.Error?.Code);
        }
    }
}
