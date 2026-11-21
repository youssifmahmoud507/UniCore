using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Curriculum;
using UniCore.Application.Modules.Graduation;
using UniCore.Application.Modules.Transcripts;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Curriculum;
using UniCore.Domain.Modules.Graduation;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.Offerings;
using UniCore.UnitTests.Modules.People;
using UniCore.UnitTests.Modules.Registrations;
using UniCore.UnitTests.Modules.University;
using UniCore.Domain.Modules.Transcripts;

namespace UniCore.UnitTests.Modules.Graduation
{
    // The GPA service answered from figures the test sets (the real one is covered by the Transcripts tests).
    internal sealed class FakeGpa : IGpaCalculationService
    {
        public GpaFigures Figures { get; set; } = new(3.0m, 6, 6, AcademicStanding.GoodStanding);
        public List<Guid> Passed { get; } = [];

        public Task<Result<StudentGpaInfo>> GetCumulativeAsync(Guid studentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<StudentGpaInfo>.Ok(new StudentGpaInfo(studentId, Figures, 0)));

        public Task<Result<SemesterGpaInfo>> GetSemesterAsync(Guid studentId, Guid semesterId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<SemesterGpaInfo>.Fail(TranscriptErrors.NoGrades));

        public Task<Result<IReadOnlyList<Guid>>> GetPassedCourseIdsAsync(Guid studentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<Guid>>.Ok([.. Passed]));
    }

    // Study plan versions answered from a dictionary, so a test can prove which version was read.
    internal sealed class GraduationPlans : IStudyPlanReadService
    {
        public Dictionary<Guid, List<PlanCourseInfo>> Versions { get; } = [];
        public Guid? LastVersionRead { get; private set; }

        public Task<Result<StudyPlanVersionInfo>> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<StudyPlanVersionInfo>.Fail(CommonErrors.DatabaseUpdateFailed));

        public Task<Result<StudyPlanVersionInfo>> GetEffectiveVersionAsync(Guid studyPlanId, DateOnly date, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<StudyPlanVersionInfo>.Fail(CommonErrors.DatabaseUpdateFailed));

        public Task<Result<bool>> ContainsCourseAsync(Guid versionId, Guid courseVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Ok(Versions.TryGetValue(versionId, out var list) && list.Any(c => c.CourseVersionId == courseVersionId)));

        public Task<Result<IReadOnlyList<PlanCourseInfo>>> GetCoursesAsync(Guid versionId, CancellationToken cancellationToken = default)
        {
            LastVersionRead = versionId;
            IReadOnlyList<PlanCourseInfo> list = Versions.TryGetValue(versionId, out var found) ? [.. found] : [];

            return Task.FromResult(Result<IReadOnlyList<PlanCourseInfo>>.Ok(list));
        }

        public Task<Result> VerifyVersionForProgramAsync(Guid versionId, Guid academicProgramId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Ok());
    }

    internal sealed class GraduationTestEnv
    {
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");
        public static readonly Guid DepartmentId = Guid.NewGuid();
        public static readonly DateTimeOffset Start = new(2027, 6, 20, 9, 0, 0, TimeSpan.Zero);

        public FakeClock Clock { get; } = new(Start);
        public FakeAcademicClock AcademicClock { get; } = new() { Today = new DateOnly(2027, 6, 20) };
        public RbacTestEnv Rbac { get; } = new();
        public FakeGpa Gpa { get; } = new();
        public GraduationPlans Plans { get; } = new();
        public FakeCourseCatalog Catalog { get; } = new();
        public FakeFinance Finance { get; } = new();
        public FakeAggregateRepository<Student> Students { get; } = new();
        public FakeEntityRepository<AcademicProgram> Programs { get; } = new();
        public FakeAggregateRepository<GraduationApplication> Applications { get; } = new();

        public GraduationEligibilityService Eligibility { get; }
        public GraduationApplicationHandler Handler { get; }
        public GraduationDecisionHandler Decisions { get; }

        public AcademicProgram Program { get; }
        public Guid PlanVersionId { get; } = Guid.NewGuid();

        // Two required courses and one elective in the plan; both required ones must be passed.
        public CourseVersionInfo Cs101 { get; }
        public CourseVersionInfo Cs102 { get; }
        public CourseVersionInfo Elective { get; }

        public GraduationTestEnv()
        {
            Program = ResultAssert.Succeeded(AcademicProgram.Create(DepartmentId, "BSC-CS", "BSc Computer Science", DegreeType.Bachelor, 4, 6, Clock.UtcNow));
            Programs.Items.Add(Program);

            Cs101 = SeedCourse("CS101");
            Cs102 = SeedCourse("CS102");
            Elective = SeedCourse("EL201");

            Plans.Versions[PlanVersionId] =
            [
                new PlanCourseInfo(Cs101.CourseVersionId, CourseRequirementType.Required, null, null, null),
                new PlanCourseInfo(Cs102.CourseVersionId, CourseRequirementType.Required, null, null, null),
                new PlanCourseInfo(Elective.CourseVersionId, CourseRequirementType.Elective, null, null, null)
            ];

            Gpa.Passed.AddRange([Cs101.CourseId, Cs102.CourseId]);

            var access = new GraduationAccess(Rbac.Authorizer, Students, Programs);
            Eligibility = new GraduationEligibilityService(Gpa, Plans, Catalog, Programs, Students);
            Handler = new GraduationApplicationHandler(access, Eligibility, Applications, Rbac.User, Rbac.Uow, Clock, Rbac.Audit);
            Decisions = new GraduationDecisionHandler(access, Eligibility, Finance, Applications, Students, Rbac.User, AcademicClock, Rbac.Uow, Clock, Rbac.Audit);
        }

        private CourseVersionInfo SeedCourse(string code)
        {
            var info = new CourseVersionInfo(Guid.NewGuid(), Guid.NewGuid(), code, DepartmentId, 1, "Title " + code, 3, 2, 2, CourseVersionStatus.Active, new DateOnly(2026, 1, 1), null);
            Catalog.Versions[info.CourseVersionId] = info;

            return info;
        }

        public Student SeedStudent(Guid? planVersionId = null)
        {
            var student = ResultAssert.Succeeded(Student.Create(
                Guid.NewGuid(), "2023CS" + Random.Shared.Next(1000, 9999), new DateOnly(2023, 9, 1), null, Guid.NewGuid(), Program.Id, planVersionId ?? PlanVersionId, null, Clock.UtcNow));
            ResultAssert.Succeeded(student.LinkAccount(Guid.NewGuid(), Clock.UtcNow));
            Students.Items.Add(student);

            return student;
        }

        public void AsUser(Guid userId, string role, string[] permissions, params ScopeGrant[] grants)
            => Rbac.SignIn(userId, TestGrants.Role(role, permissions, grants));

        public void AsStudent(Student student)
            => AsUser(student.UserId!.Value, RoleNames.Student, [Permissions.Graduation.Read, Permissions.Graduation.Apply], new ScopeGrant(ScopeType.Department, Guid.NewGuid()));

        public void AsRegistrar()
            => AsUser(Guid.NewGuid(), RoleNames.Registrar, [Permissions.Graduation.Read, Permissions.Graduation.Decide], new ScopeGrant(ScopeType.Global, null));

        // A department head of the program's own department: reads, never decides.
        public void AsHead(Guid? departmentId = null)
            => AsUser(Guid.NewGuid(), RoleNames.DepartmentHead, [Permissions.Graduation.Read, Permissions.Graduation.Decide], new ScopeGrant(ScopeType.Department, departmentId ?? DepartmentId));
    }
}
