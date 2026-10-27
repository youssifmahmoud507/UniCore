using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Offerings;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.People;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.University;

namespace UniCore.UnitTests.Modules.Offerings
{
    // The Courses contract answered from a dictionary.
    internal sealed class FakeCourseCatalog : ICourseCatalogReadService
    {
        public Dictionary<Guid, CourseVersionInfo> Versions { get; } = [];

        public Task<Result<CourseInfo>> GetCourseAsync(Guid courseId, CancellationToken cancellationToken = default)
        {
            var found = Versions.Values.FirstOrDefault(v => v.CourseId == courseId);

            return Task.FromResult<Result<CourseInfo>>(found is null
                ? CoursesErrors.CourseNotFound
                : new CourseInfo(found.CourseId, found.DepartmentId, found.CourseCode, found.Title, CourseType.Mixed, true));
        }

        public Task<Result<CourseVersionInfo>> GetVersionAsync(Guid courseVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult<Result<CourseVersionInfo>>(Versions.TryGetValue(courseVersionId, out var info) ? info : CoursesErrors.VersionNotFound);

        public Task<Result<CourseVersionInfo>> GetEffectiveVersionAsync(Guid courseId, DateOnly date, CancellationToken cancellationToken = default)
            => Task.FromResult<Result<CourseVersionInfo>>(CoursesErrors.VersionNotFound);
    }

    internal sealed class FakeEnrollmentCounter : ISectionEnrollmentCounter
    {
        public Dictionary<Guid, int> Counts { get; } = [];

        public Task<int> CountEnrolledAsync(Guid courseSectionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Counts.TryGetValue(courseSectionId, out var count) ? count : 0);
    }

    // The real offerings handlers and the real PermissionAuthorizer, wired to in-memory fakes.
    internal sealed class OfferingsTestEnv
    {
        public static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");

        // Fall 2026: 1 Sep - 31 Jan.
        public static readonly DateOnly SemesterStart = new(2026, 9, 1);
        public static readonly DateOnly SemesterEnd = new(2027, 1, 31);

        public FakeClock Clock { get; } = new(Start);
        public RbacTestEnv Rbac { get; } = new();

        public FakeAggregateRepository<CourseOffering> Offerings { get; } = new();
        public FakeAggregateRepository<CourseSection> Sections { get; } = new();
        public FakeAggregateRepository<InstructorAssignment> Assignments { get; } = new();
        public FakeEntityRepository<AcademicSemester> Semesters { get; } = new();
        public FakeEntityRepository<Instructor> Instructors { get; } = new();
        public FakeCourseCatalog Catalog { get; } = new();
        public FakeEnrollmentCounter Enrollments { get; } = new();

        public OfferingAccess Access { get; }
        public CourseOfferingHandler OfferingHandler { get; }
        public CourseSectionHandler SectionHandler { get; }
        public InstructorAssignmentHandler AssignmentHandler { get; }
        public OfferingReadService ReadService { get; }

        public Guid ActorId { get; private set; }
        public Guid DepartmentId { get; } = Guid.NewGuid();

        public OfferingsTestEnv()
        {
            Access = new OfferingAccess(Rbac.Authorizer, Offerings, Sections, Semesters, Catalog);
            OfferingHandler = new CourseOfferingHandler(Rbac.Authorizer, Rbac.User, Access, Offerings, Sections, Assignments, Enrollments, Rbac.Uow, Clock, Rbac.Audit);
            SectionHandler = new CourseSectionHandler(Rbac.Authorizer, Rbac.User, Access, Sections, Offerings, Assignments, Enrollments, Rbac.Uow, Clock, Rbac.Audit);
            AssignmentHandler = new InstructorAssignmentHandler(Rbac.Authorizer, Rbac.User, Access, Assignments, Sections, Instructors, Rbac.Uow, Clock, Rbac.Audit);
            ReadService = new OfferingReadService(Offerings, Sections, Assignments, Catalog);
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

        // A course version of this environment's department. Draft versions are never effective.
        public CourseVersionInfo SeedVersion(string code = "CS101", CourseVersionStatus status = CourseVersionStatus.Active, DateOnly? from = null, DateOnly? to = null, int credits = 3)
        {
            var info = new CourseVersionInfo(Guid.NewGuid(), Guid.NewGuid(), code, DepartmentId, 1, "Title " + code, credits, 2, 2, status, from ?? new DateOnly(2026, 1, 1), to);
            Catalog.Versions[info.CourseVersionId] = info;
            return info;
        }

        public AcademicSemester SeedSemester(bool closed = false)
        {
            var windows = new SemesterWindows(
                RegistrationStart: new(2026, 9, 1), RegistrationEnd: new(2026, 9, 10),
                AddDropStart: new(2026, 9, 5), AddDropEnd: new(2026, 9, 20),
                WithdrawalDeadline: new(2026, 11, 15),
                MidtermStart: new(2026, 11, 1), MidtermEnd: new(2026, 11, 10),
                FinalExamStart: new(2027, 1, 5), FinalExamEnd: new(2027, 1, 20),
                GradeSubmissionDeadline: new(2027, 1, 28), GradePublicationDate: new(2027, 2, 3));

            var semester = ResultAssert.Succeeded(AcademicSemester.Create(Guid.NewGuid(), Guid.NewGuid(), SemesterStart, SemesterEnd, windows, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31), Clock.UtcNow));

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

        // An offering created through the real handler, so it follows the same rules as the API.
        public async Task<OfferingSummary> CreateOfferingAsync(Guid courseVersionId, Guid semesterId, int? maxSections = null)
            => ResultAssert.Succeeded(await OfferingHandler.CreateAsync(new CreateOfferingCommand(courseVersionId, semesterId, maxSections), Request));

        public async Task<SectionSummary> CreateSectionAsync(Guid offeringId, string code = "A", int capacity = 30)
            => ResultAssert.Succeeded(await SectionHandler.CreateAsync(offeringId, new CreateSectionCommand(code, capacity), Request));

        // An offering with one section, opened.
        public async Task<(OfferingSummary Offering, SectionSummary Section)> OpenOfferingAsync(Guid courseVersionId, Guid semesterId)
        {
            var offering = await CreateOfferingAsync(courseVersionId, semesterId);
            var section = await CreateSectionAsync(offering.Id);
            var opened = ResultAssert.Succeeded(await OfferingHandler.OpenAsync(offering.Id, Request));
            return (opened, section);
        }

        public async Task<AssignmentSummary> AssignAsync(Guid sectionId, Guid instructorId, AssignmentRole role)
            => ResultAssert.Succeeded(await AssignmentHandler.AssignAsync(sectionId, new AssignInstructorCommand(instructorId, role), Request));
    }
}
