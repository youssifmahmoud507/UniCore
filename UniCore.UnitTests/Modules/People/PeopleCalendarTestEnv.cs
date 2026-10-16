using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Calendar;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.People;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.People;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.University;

namespace UniCore.UnitTests.Modules.People
{
    internal sealed class FakeAcademicClock : IAcademicClock
    {
        public DateOnly Today { get; set; } = new(2026, 10, 12);
    }

    internal sealed class FakeSectionAssignmentReader : ISectionAssignmentReader
    {
        public List<Guid> Sections { get; } = [];
        public Guid? LastInstructorId { get; private set; }
        public Guid? LastAssistantId { get; private set; }

        public Task<IReadOnlyList<Guid>> GetAssignedSectionIdsAsync(Guid? instructorId, Guid? teachingAssistantId, CancellationToken cancellationToken = default)
        {
            LastInstructorId = instructorId;
            LastAssistantId = teachingAssistantId;
            return Task.FromResult<IReadOnlyList<Guid>>([.. Sections]);
        }
    }

    // The real calendar and people handlers and the real PermissionAuthorizer, wired to in-memory fakes.
    internal sealed class PeopleCalendarTestEnv
    {
        public static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");

        public FakeClock Clock { get; } = new(Start);
        public FakeAcademicClock AcademicClock { get; } = new();
        public RbacTestEnv Rbac { get; } = new();

        public FakeEntityRepository<AcademicYear> Years { get; } = new();
        public FakeEntityRepository<AcademicTerm> Terms { get; } = new();
        public FakeEntityRepository<AcademicSemester> Semesters { get; } = new();
        public FakeEntityRepository<AcademicLevel> Levels { get; } = new();
        public FakeEntityRepository<AcademicProgram> Programs { get; } = new();
        public FakeEntityRepository<Department> Departments { get; } = new();
        public FakeEntityRepository<Faculty> Faculties { get; } = new();
        public FakeEntityRepository<PersonProfile> Persons { get; } = new();
        public FakeEntityRepository<Employee> Employees { get; } = new();
        public FakeEntityRepository<Instructor> Instructors { get; } = new();
        public FakeEntityRepository<TeachingAssistant> Assistants { get; } = new();
        public FakeEntityRepository<Student> Students { get; } = new();
        public FakeSectionAssignmentReader SectionReader { get; } = new();

        public AcademicYearHandler YearHandler { get; }
        public AcademicTermHandler TermHandler { get; }
        public AcademicSemesterHandler SemesterHandler { get; }
        public AcademicLevelHandler LevelHandler { get; }
        public IAcademicCalendarService Calendar { get; }
        public PersonProfileHandler PersonHandler { get; }
        public EmployeeHandler EmployeeHandler { get; }
        public InstructorHandler InstructorHandler { get; }
        public TeachingAssistantHandler AssistantHandler { get; }
        public IScopeDataProvider ScopeData { get; }

        public PeopleCalendarTestEnv()
        {
            var authorizer = Rbac.Authorizer;
            var user = Rbac.User;

            YearHandler = new AcademicYearHandler(authorizer, user, Years, Semesters, Rbac.Uow, Clock, Rbac.Audit);
            TermHandler = new AcademicTermHandler(authorizer, user, Terms, Rbac.Uow, Rbac.Audit);
            SemesterHandler = new AcademicSemesterHandler(authorizer, user, Semesters, Years, Terms, Rbac.Uow, Clock, Rbac.Audit);
            LevelHandler = new AcademicLevelHandler(authorizer, user, Levels, Programs, Rbac.Uow, Clock, Rbac.Audit);
            Calendar = new AcademicCalendarService(Years, Semesters, AcademicClock);

            PersonHandler = new PersonProfileHandler(authorizer, user, Persons, Rbac.Uow, Clock, Rbac.Audit);
            EmployeeHandler = new EmployeeHandler(authorizer, user, Employees, Persons, Departments, Rbac.Uow, Clock, Rbac.Audit);
            InstructorHandler = new InstructorHandler(authorizer, user, Instructors, Employees, Rbac.Uow, Clock, Rbac.Audit);
            AssistantHandler = new TeachingAssistantHandler(authorizer, user, Assistants, Employees, Students, Rbac.Uow, Clock, Rbac.Audit);
            ScopeData = new ScopeDataProvider(Persons, Employees, Instructors, Assistants, Students, Departments, Faculties, SectionReader);
        }

        // ---------- who is calling ----------

        public Guid ActorId { get; private set; }

        public void AsSuperAdmin()
        {
            ActorId = Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.SuperAdmin());
        }

        public void AsReader()
        {
            ActorId = Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.Role(RoleNames.Registrar, [Permissions.University.Read, Permissions.People.Read], new ScopeGrant(ScopeType.Global, null)));
        }

        // Manages University and People data, but only inside one faculty.
        public void AsDeanOf(Guid facultyId)
        {
            ActorId = Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.Role(
                RoleNames.Dean,
                [Permissions.University.Read, Permissions.University.Manage, Permissions.People.Read, Permissions.People.Manage],
                new ScopeGrant(ScopeType.Faculty, facultyId)));
        }

        // The user can read people but not manage them.
        public void AsPeopleReader(Guid? userId = null)
        {
            ActorId = userId ?? Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.Role(RoleNames.Registrar, [Permissions.People.Read], new ScopeGrant(ScopeType.Global, null)));
        }

        public void PlaceDepartmentInFaculty(Guid departmentId, Guid facultyId)
        {
            Rbac.Hierarchy.Chains[(ScopeType.Department, departmentId)] =
                [new ScopeRef(ScopeType.Department, departmentId), new ScopeRef(ScopeType.Faculty, facultyId)];
        }

        // ---------- seed data (through the real factories) ----------

        public AcademicYear SeedYear(string name = "2026/2027", int startYear = 2026, bool active = false, bool current = false)
        {
            var year = ResultAssert.Succeeded(AcademicYear.Create(name, new DateOnly(startYear, 9, 1), new DateOnly(startYear + 1, 8, 31), Clock.UtcNow));
            if (active || current)
            {
                ResultAssert.Succeeded(year.Activate(Clock.UtcNow));
            }

            if (current)
            {
                ResultAssert.Succeeded(year.MakeCurrent(false, Clock.UtcNow));
            }

            Years.Items.Add(year);
            return year;
        }

        public AcademicTerm SeedTerm(string code = "FALL", int order = 1, bool active = true)
        {
            var term = ResultAssert.Succeeded(AcademicTerm.Create(code, code, order));
            if (!active)
            {
                ResultAssert.Succeeded(term.Deactivate());
            }

            Terms.Items.Add(term);
            return term;
        }

        public AcademicSemester SeedSemester(AcademicYear year, AcademicTerm term, DateOnly start, DateOnly end, SemesterWindows? windows = null)
        {
            var semester = ResultAssert.Succeeded(AcademicSemester.Create(year.Id, term.Id, start, end, windows, year.StartDate, year.EndDate, Clock.UtcNow));
            Semesters.Items.Add(semester);
            return semester;
        }

        public Faculty SeedFaculty(string code = "ENG")
        {
            var faculty = ResultAssert.Succeeded(Faculty.Create(Guid.NewGuid(), code, "Faculty " + code, null, null, null, Clock.UtcNow));
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

        public PersonProfile SeedPerson(string first = "Nour", string last = "Ahmed", string? nationalId = null, Guid? userId = null)
        {
            var person = ResultAssert.Succeeded(PersonProfile.Create(first, last, null, null, nationalId, null, null, Clock.UtcToday, Clock.UtcNow));
            if (userId is { } id)
            {
                ResultAssert.Succeeded(person.LinkUser(id, Clock.UtcNow));
            }

            Persons.Items.Add(person);
            return person;
        }

        public Employee SeedEmployee(Guid personId, string number = "EMP-001", Guid? departmentId = null)
        {
            var employee = ResultAssert.Succeeded(Employee.Create(personId, number, departmentId, null, new DateOnly(2024, 9, 1), Clock.UtcNow));
            Employees.Items.Add(employee);
            return employee;
        }

        public Instructor SeedInstructor(Guid employeeId, bool active = true)
        {
            var instructor = ResultAssert.Succeeded(Instructor.Create(employeeId, AcademicRank.Lecturer, null, Clock.UtcNow));
            if (!active)
            {
                ResultAssert.Succeeded(instructor.Deactivate(Clock.UtcNow));
            }

            Instructors.Items.Add(instructor);
            return instructor;
        }

        public Student SeedStudent(Guid personId, string number = "2026001")
        {
            var student = ResultAssert.Succeeded(Student.Create(personId, number, new DateOnly(2026, 9, 1), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Clock.UtcNow));
            Students.Items.Add(student);
            return student;
        }
    }
}
