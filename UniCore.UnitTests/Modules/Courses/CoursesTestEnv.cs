using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.University;

namespace UniCore.UnitTests.Modules.Courses
{
    // The real courses handlers and the real PermissionAuthorizer, wired to in-memory fakes.
    internal sealed class CoursesTestEnv
    {
        public static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");
        public static readonly DateOnly From = new(2026, 9, 1);

        public FakeClock Clock { get; } = new(Start);
        public RbacTestEnv Rbac { get; } = new();

        public FakeAggregateRepository<Course> Courses { get; } = new();
        public FakeAggregateRepository<CourseVersion> Versions { get; } = new();
        public FakeEntityRepository<Department> Departments { get; } = new();

        public CourseHandler CourseHandler { get; }
        public CourseVersionHandler VersionHandler { get; }
        public CourseCatalogReadService ReadService { get; }

        public Guid ActorId { get; private set; }

        public CoursesTestEnv()
        {
            CourseHandler = new CourseHandler(Rbac.Authorizer, Rbac.User, Courses, Versions, Departments, Rbac.Uow, Clock, Rbac.Audit);
            VersionHandler = new CourseVersionHandler(Rbac.Authorizer, Rbac.User, Versions, Courses, Rbac.Uow, Clock, Rbac.Audit);
            ReadService = new CourseCatalogReadService(Courses, Versions);
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

        // Holds Courses.Manage, but only inside one faculty.
        public void AsDeanOf(Guid facultyId)
        {
            ActorId = Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.Role(RoleNames.Dean, [Permissions.Courses.Read, Permissions.Courses.Manage], new ScopeGrant(ScopeType.Faculty, facultyId)));
        }

        public void PlaceDepartmentInFaculty(Guid departmentId, Guid facultyId)
        {
            Rbac.Hierarchy.Chains[(ScopeType.Department, departmentId)] =
                [new ScopeRef(ScopeType.Department, departmentId), new ScopeRef(ScopeType.Faculty, facultyId)];
        }

        // ---------- seed data (built through the real factories) ----------

        public Department SeedDepartment(string code = "CSE", bool active = true)
        {
            var department = ResultAssert.Succeeded(Department.Create(Guid.NewGuid(), code, "Department " + code, null, null, null));

            if (!active)
            {
                ResultAssert.Succeeded(department.Deactivate(false));
            }

            Departments.Items.Add(department);
            return department;
        }

        public Course SeedCourse(Guid departmentId, string code = "CS101")
        {
            var course = ResultAssert.Succeeded(Course.Create(departmentId, code, "Course " + code, null, CourseType.Mixed, Clock.UtcNow));
            Courses.Items.Add(course);
            return course;
        }

        // A draft created through the real handler, so it follows the same rules as the API.
        public async Task<CourseVersionSummary> CreateDraftAsync(Guid courseId, DateOnly? from = null, Guid? sourceVersionId = null, int credits = 3)
            => ResultAssert.Succeeded(await VersionHandler.CreateAsync(courseId, new CreateCourseVersionCommand(sourceVersionId, sourceVersionId is null ? "Title" : null, null, sourceVersionId is null ? credits : null, sourceVersionId is null ? 2 : null, sourceVersionId is null ? 2 : null, from ?? From), Request));

        public async Task<CourseVersionSummary> ActiveVersionAsync(Guid courseId, DateOnly? from = null)
        {
            var draft = await CreateDraftAsync(courseId, from);
            return ResultAssert.Succeeded(await VersionHandler.ActivateAsync(draft.Id, Request));
        }
    }
}
