using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Assessments;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Offerings;
using UniCore.Domain.Modules.Assessments;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Offerings;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.Offerings;
using UniCore.UnitTests.Modules.Scheduling;
using UniCore.UnitTests.Modules.University;

namespace UniCore.UnitTests.Modules.Assessments
{
    // The real assessment handler, read service and PermissionAuthorizer, wired to in-memory fakes.
    // Fall 2026: 1 Sep 2026 - 31 Jan 2027, midterms 1-10 Nov 2026, finals 5-20 Jan 2027.
    internal sealed class AssessmentsTestEnv
    {
        public static readonly DateTimeOffset Start = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");
        public static readonly Guid DepartmentId = Guid.NewGuid();

        public FakeClock Clock { get; } = new(Start);
        public RbacTestEnv Rbac { get; } = new();

        public FakeAggregateRepository<Assessment> Assessments { get; } = new();
        public FakeEntityRepository<AcademicSemester> Semesters { get; } = new();
        public FakeSchedulingOfferings Offerings { get; } = new();
        public FakeCourseCatalog Catalog { get; } = new();

        public AssessmentAccess Access { get; }
        public AssessmentHandler Handler { get; }
        public AssessmentReadService Reader { get; }

        public AcademicSemester Semester { get; }

        public AssessmentsTestEnv()
        {
            Access = new AssessmentAccess(Rbac.Authorizer, Offerings, Catalog, Semesters);
            Handler = new AssessmentHandler(Access, Rbac.User, Assessments, Rbac.Uow, Clock, Rbac.Audit);
            Reader = new AssessmentReadService(Assessments);

            Semester = SeedSemester();
        }

        // ---------- who is calling ----------

        // An instructor holds a Section grant for each section they teach.
        public void AsInstructor(params Guid[] sectionIds)
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Instructor, [Permissions.Assessments.Read, Permissions.Assessments.Manage], [.. sectionIds.Select(id => new ScopeGrant(ScopeType.Section, id))]));

        public void AsHead(Guid? departmentId = null)
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.DepartmentHead, [Permissions.Assessments.Read, Permissions.Assessments.Manage], new ScopeGrant(ScopeType.Department, departmentId ?? DepartmentId)));

        // The registrar reads every scheme and changes none.
        public void AsRegistrar()
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Registrar, [Permissions.Assessments.Read], new ScopeGrant(ScopeType.Global, null)));

        public void AsStudent()
            => Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Student, [Permissions.Grades.Read], new ScopeGrant(ScopeType.Global, null)));

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

            return info;
        }

        // ---------- calls ----------

        public async Task<AssessmentSummary> CreateOkAsync(SectionInfo section)
        {
            AsHead();
            var result = await Handler.CreateAsync(new CreateAssessmentCommand(section.SectionId), Request);
            Assert.True(result.IsSuccess, result.Error?.Code);

            return result.Value!;
        }

        public static ComponentCommand Quiz(string name = "Quiz 1", decimal weight = 10, DateOnly? due = null)
            => new(name, ComponentType.Quiz, weight, 20, due ?? new DateOnly(2026, 10, 15));

        public async Task<AssessmentSummary> AddOkAsync(AssessmentSummary assessment, ComponentCommand command)
        {
            AsHead();
            var result = await Handler.AddComponentAsync(assessment.Id, command, Request);
            Assert.True(result.IsSuccess, result.Error?.Code);

            return result.Value!;
        }

        // Quiz 20 + Midterm 30 + Final 50 = 100, all with due dates.
        public async Task<AssessmentSummary> FillToHundredAsync(AssessmentSummary assessment)
        {
            await AddOkAsync(assessment, Quiz("Quizzes", 20));
            await AddOkAsync(assessment, new ComponentCommand("Midterm", ComponentType.Midterm, 30, 50, new DateOnly(2026, 11, 5)));

            return await AddOkAsync(assessment, new ComponentCommand("Final", ComponentType.Final, 50, 100, new DateOnly(2027, 1, 10)));
        }
    }
}
