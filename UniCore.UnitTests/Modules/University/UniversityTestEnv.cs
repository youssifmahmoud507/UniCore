using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.University;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Identity;

using UniversityEntity = UniCore.Domain.Modules.University.University;

namespace UniCore.UnitTests.Modules.University
{
    // The real handlers and the real PermissionAuthorizer, wired to in-memory fakes.
    internal sealed class UniversityTestEnv
    {
        public static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");

        public FakeClock Clock { get; } = new(Start);
        public RbacTestEnv Rbac { get; } = new();

        public FakeEntityRepository<UniversityEntity> Universities { get; } = new();
        public FakeEntityRepository<Campus> Campuses { get; } = new();
        public FakeEntityRepository<Building> Buildings { get; } = new();
        public FakeEntityRepository<Faculty> Faculties { get; } = new();
        public FakeEntityRepository<Department> Departments { get; } = new();
        public FakeEntityRepository<AcademicProgram> Programs { get; } = new();
        public FakeProgramUsageChecker Usage { get; } = new();

        public UniversityManagementHandler UniversityHandler { get; }
        public CampusManagementHandler CampusHandler { get; }
        public FacultyManagementHandler FacultyHandler { get; }
        public DepartmentManagementHandler DepartmentHandler { get; }
        public ProgramManagementHandler ProgramHandler { get; }

        public UniversityTestEnv()
        {
            var authorizer = Rbac.Authorizer;
            var user = Rbac.User;

            UniversityHandler = new UniversityManagementHandler(authorizer, user, Universities, Faculties, Campuses, Rbac.Uow, Clock, Rbac.Audit);
            CampusHandler = new CampusManagementHandler(authorizer, user, Campuses, Universities, Buildings, Rbac.Uow, Clock, Rbac.Audit);
            FacultyHandler = new FacultyManagementHandler(authorizer, user, Faculties, Universities, Departments, Rbac.Uow, Clock, Rbac.Audit);
            DepartmentHandler = new DepartmentManagementHandler(authorizer, user, Departments, Faculties, Programs, Rbac.Uow, Clock, Rbac.Audit);
            ProgramHandler = new ProgramManagementHandler(authorizer, user, Programs, Departments, Usage, Rbac.Uow, Clock, Rbac.Audit);
        }

        // ---------- who is calling ----------

        public Guid ActorId { get; private set; }

        public void AsSuperAdmin()
        {
            ActorId = Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.SuperAdmin());
        }

        // Reads everything, changes nothing.
        public void AsReader()
        {
            ActorId = Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.Role(RoleNames.Registrar, [Permissions.University.Read], new ScopeGrant(ScopeType.Global, null)));
        }

        // Holds University.Manage, but only inside one faculty.
        public void AsDeanOf(Guid facultyId)
        {
            ActorId = Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.Role(RoleNames.Dean, [Permissions.University.Read, Permissions.University.Manage], new ScopeGrant(ScopeType.Faculty, facultyId)));
        }

        // Tells the fake hierarchy which faculty a department sits in (the real one reads the database).
        public void PlaceDepartmentInFaculty(Guid departmentId, Guid facultyId)
        {
            Rbac.Hierarchy.Chains[(ScopeType.Department, departmentId)] =
                [new ScopeRef(ScopeType.Department, departmentId), new ScopeRef(ScopeType.Faculty, facultyId)];
        }

        // ---------- seed data (built through the real factories) ----------

        public UniversityEntity SeedUniversity(string code = "CU", string name = "Cairo University", bool active = true)
        {
            var university = ResultAssert.Succeeded(UniversityEntity.Create(code, name, null, null, null, null, Clock.UtcNow));

            if (!active)
            {
                ResultAssert.Succeeded(university.Deactivate(false, Clock.UtcNow));
            }

            Universities.Items.Add(university);
            return university;
        }

        public Faculty SeedFaculty(Guid universityId, string code = "ENG", bool active = true)
        {
            var faculty = ResultAssert.Succeeded(Faculty.Create(universityId, code, "Faculty " + code, null, null, null, Clock.UtcNow));

            if (!active)
            {
                ResultAssert.Succeeded(faculty.Deactivate(false, Clock.UtcNow));
            }

            Faculties.Items.Add(faculty);
            return faculty;
        }

        public Department SeedDepartment(Guid facultyId, string code = "CSE", bool active = true)
        {
            var department = ResultAssert.Succeeded(Department.Create(facultyId, code, "Department " + code, null, null, null));

            if (!active)
            {
                ResultAssert.Succeeded(department.Deactivate(false));
            }

            Departments.Items.Add(department);
            return department;
        }

        public AcademicProgram SeedProgram(Guid departmentId, string code = "BSC-CSE", bool active = true)
        {
            var program = ResultAssert.Succeeded(AcademicProgram.Create(departmentId, code, "Program " + code, DegreeType.Bachelor, 5, 180, Clock.UtcNow));

            if (!active)
            {
                ResultAssert.Succeeded(program.Deactivate(false, Clock.UtcNow));
            }

            Programs.Items.Add(program);
            return program;
        }
    }
}
