using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Curriculum;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Curriculum;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.University;

namespace UniCore.UnitTests.Modules.Curriculum
{
    internal sealed class FakeCourseVersionVerifier : ICourseVersionVerifier
    {
        public HashSet<Guid> Unknown { get; } = [];

        public Task<Result> VerifyAsync(Guid courseVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Unknown.Contains(courseVersionId) ? Result.Fail(CurriculumErrors.CourseVersionNotFound) : Result.Ok());
    }

    // The real curriculum handlers and the real PermissionAuthorizer, wired to in-memory fakes.
    internal sealed class CurriculumTestEnv
    {
        public static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");

        public FakeClock Clock { get; } = new(Start);
        public RbacTestEnv Rbac { get; } = new();

        public FakeAggregateRepository<StudyPlan> Plans { get; } = new();
        public FakeAggregateRepository<StudyPlanVersion> Versions { get; } = new();
        public FakeEntityRepository<AcademicProgram> Programs { get; } = new();
        public FakeEntityRepository<AcademicLevel> Levels { get; } = new();
        public FakeEntityRepository<AcademicTerm> Terms { get; } = new();
        public FakeCourseVersionVerifier CourseVersions { get; } = new();

        public StudyPlanHandler PlanHandler { get; }
        public StudyPlanVersionHandler VersionHandler { get; }
        public StudyPlanReadService ReadService { get; }

        public Guid ActorId { get; private set; }

        public CurriculumTestEnv()
        {
            PlanHandler = new StudyPlanHandler(Rbac.Authorizer, Rbac.User, Plans, Versions, Programs, Rbac.Uow, Clock, Rbac.Audit);
            VersionHandler = new StudyPlanVersionHandler(Rbac.Authorizer, Rbac.User, Versions, Plans, Programs, Levels, Terms, CourseVersions, Rbac.Uow, Clock, Rbac.Audit);
            ReadService = new StudyPlanReadService(Versions, Plans);
        }

        // ---------- who is calling ----------

        public void AsSuperAdmin()
        {
            ActorId = Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.SuperAdmin());
        }

        // Reads, changes nothing.
        public void AsReader()
        {
            ActorId = Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.Role(RoleNames.Registrar, [Permissions.Courses.Read], new ScopeGrant(ScopeType.Global, null)));
        }

        // Holds Curriculum.Manage, but only inside one faculty.
        public void AsDeanOf(Guid facultyId)
        {
            ActorId = Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.Role(RoleNames.Dean, [Permissions.Courses.Read, Permissions.Curriculum.Manage], new ScopeGrant(ScopeType.Faculty, facultyId)));
        }

        public void PlaceDepartmentInFaculty(Guid departmentId, Guid facultyId)
        {
            Rbac.Hierarchy.Chains[(ScopeType.Department, departmentId)] =
                [new ScopeRef(ScopeType.Department, departmentId), new ScopeRef(ScopeType.Faculty, facultyId)];
        }

        // ---------- seed data (built through the real factories) ----------

        public AcademicProgram SeedProgram(Guid? departmentId = null, string code = "BSC-CSE", bool active = true)
        {
            var program = ResultAssert.Succeeded(AcademicProgram.Create(departmentId ?? Guid.NewGuid(), code, "Program " + code, DegreeType.Bachelor, 5, 180, Clock.UtcNow));

            if (!active)
            {
                ResultAssert.Succeeded(program.Deactivate(false, Clock.UtcNow));
            }

            Programs.Items.Add(program);
            return program;
        }

        public AcademicLevel SeedLevel(Guid programId, int order = 1, bool active = true)
        {
            var level = ResultAssert.Succeeded(AcademicLevel.Create(programId, "Level " + order, order, 0, Clock.UtcNow));

            if (!active)
            {
                ResultAssert.Succeeded(level.Deactivate(Clock.UtcNow));
            }

            Levels.Items.Add(level);
            return level;
        }

        public AcademicTerm SeedTerm(string code = "FALL", bool active = true)
        {
            var term = ResultAssert.Succeeded(AcademicTerm.Create(code, "Term " + code, 1));

            if (!active)
            {
                ResultAssert.Succeeded(term.Deactivate());
            }

            Terms.Items.Add(term);
            return term;
        }

        public StudyPlan SeedPlan(Guid programId, string code = "PLAN-2026")
        {
            var plan = ResultAssert.Succeeded(StudyPlan.Create(programId, code, "Plan " + code, null, Clock.UtcNow));
            Plans.Items.Add(plan);
            return plan;
        }

        // A draft created through the real handler, so it follows the same rules as the API.
        public async Task<VersionDetail> CreateDraftAsync(Guid planId, DateOnly? from = null, Guid? sourceVersionId = null)
            => ResultAssert.Succeeded(await VersionHandler.CreateAsync(planId, new CreateVersionCommand(sourceVersionId, from ?? new DateOnly(2026, 9, 1)), Request));

        public async Task<VersionDetail> AddCourseAsync(Guid versionId, Guid? courseVersionId = null, CourseRequirementType type = CourseRequirementType.Required)
            => ResultAssert.Succeeded(await VersionHandler.AddCourseAsync(versionId, new AddPlanCourseCommand(courseVersionId ?? Guid.NewGuid(), null, null, type, null), Request));

        // A draft with one course, activated.
        public async Task<VersionDetail> ActiveVersionAsync(Guid planId, DateOnly? from = null)
        {
            var draft = await CreateDraftAsync(planId, from);
            await AddCourseAsync(draft.Version.Id);
            return ResultAssert.Succeeded(await VersionHandler.ActivateAsync(draft.Version.Id, Request));
        }
    }
}
