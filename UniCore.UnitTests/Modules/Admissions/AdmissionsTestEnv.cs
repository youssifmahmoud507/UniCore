using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Specifications;
using UniCore.Application.Modules.Admissions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Admissions;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Identity;
using UniCore.UnitTests.Modules.University;

namespace UniCore.UnitTests.Modules.Admissions
{
    // IAggregateRepository over the shared in-memory fake (that one only implements IRepository).
    internal sealed class FakeAggregateRepository<T> : IAggregateRepository<T> where T : AggregateRoot
    {
        private readonly FakeEntityRepository<T> _inner = new();

        public List<T> Items => _inner.Items;

        public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _inner.GetByIdAsync(id, cancellationToken);
        public Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) => _inner.FirstOrDefaultAsync(specification, cancellationToken);
        public Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) => _inner.ListAsync(specification, cancellationToken);
        public Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) => _inner.CountAsync(specification, cancellationToken);
        public Task<bool> AnyAsync(ISpecification<T> specification, CancellationToken cancellationToken = default) => _inner.AnyAsync(specification, cancellationToken);
        public void Add(T entity) => _inner.Add(entity);
        public void Update(T entity) => _inner.Update(entity);
        public void Remove(T entity) => _inner.Remove(entity);
    }

    internal sealed class FakeDeadlineProvider : IAdmissionDeadlineProvider
    {
        public Dictionary<Guid, DateTimeOffset> Deadlines { get; } = [];

        public Task<Result<DateTimeOffset>> GetDeadlineAsync(Guid academicYearId, CancellationToken cancellationToken = default)
            => Task.FromResult(Deadlines.TryGetValue(academicYearId, out var deadline)
                ? Result<DateTimeOffset>.Ok(deadline)
                : Result<DateTimeOffset>.Fail(AdmissionsErrors.DeadlineNotConfigured));
    }

    internal sealed class FakeDocumentVerifier : IAdmissionDocumentVerifier
    {
        public HashSet<Guid> Unknown { get; } = [];

        public Task<Result> VerifyAsync(Guid documentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Unknown.Contains(documentId) ? Result.Fail(AdmissionsErrors.DocumentNotFound) : Result.Ok());
    }

    // The real Admissions handlers and the real PermissionAuthorizer, wired to in-memory fakes.
    internal sealed class AdmissionsTestEnv
    {
        public static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        public static readonly RequestInfo Request = new("127.0.0.1", "tests");

        public FakeClock Clock { get; } = new(Start);
        public RbacTestEnv Rbac { get; } = new();

        public FakeAggregateRepository<Applicant> Applicants { get; } = new();
        public FakeAggregateRepository<AdmissionRequirement> Requirements { get; } = new();
        public FakeAggregateRepository<AdmissionApplication> Applications { get; } = new();
        public FakeEntityRepository<AcademicProgram> Programs { get; } = new();
        public FakeDeadlineProvider Deadlines { get; } = new();
        public FakeDocumentVerifier Documents { get; } = new();

        public ApplicantHandler ApplicantHandler { get; }
        public AdmissionRequirementHandler RequirementHandler { get; }
        public AdmissionApplicationHandler ApplicationHandler { get; }
        public AdmissionExpiryService ExpiryService { get; }

        public Guid YearId { get; } = Guid.NewGuid();
        public Guid ActorId { get; private set; }

        public AdmissionsTestEnv()
        {
            var authorizer = Rbac.Authorizer;
            var user = Rbac.User;

            Deadlines.Deadlines[YearId] = Start.AddDays(60);

            ApplicantHandler = new ApplicantHandler(authorizer, user, Applicants, Rbac.Uow, Clock, Rbac.Audit);
            RequirementHandler = new AdmissionRequirementHandler(authorizer, user, Requirements, Programs, Rbac.Uow, Clock, Rbac.Audit);
            ApplicationHandler = new AdmissionApplicationHandler(authorizer, user, Applications, Applicants, Requirements, Programs, Deadlines, Documents, Rbac.Uow, Clock, Rbac.Audit);
            ExpiryService = new AdmissionExpiryService(Applications, Rbac.Uow, Clock, Rbac.Audit);
        }

        // ---------- who is calling ----------

        public void AsSuperAdmin()
        {
            ActorId = Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.SuperAdmin());
        }

        public void AsOfficer(params string[] permissions)
        {
            ActorId = Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.Role(RoleNames.AdmissionsOfficer, permissions, new ScopeGrant(ScopeType.Global, null)));
        }

        // Review + Read, no Decide.
        public void AsReviewer() => AsOfficer(Permissions.Admissions.Read, Permissions.Admissions.Review);

        // Holds all three permissions, but only inside one faculty.
        public void AsDeanOf(Guid facultyId)
        {
            ActorId = Guid.NewGuid();
            Rbac.SignIn(ActorId, TestGrants.Role(RoleNames.Dean, [Permissions.Admissions.Read, Permissions.Admissions.Review, Permissions.Admissions.Decide], new ScopeGrant(ScopeType.Faculty, facultyId)));
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

        public Applicant SeedApplicant(string email = "mona@example.com")
        {
            var applicant = ResultAssert.Succeeded(Applicant.Create("Mona Ali", email, null, new DateOnly(2008, 5, 20), Clock.UtcToday, Clock.UtcNow));
            Applicants.Items.Add(applicant);
            return applicant;
        }

        public AdmissionRequirement SeedRequirement(Guid programId, string code, bool mandatory = true, bool active = true)
        {
            var requirement = ResultAssert.Succeeded(AdmissionRequirement.Create(programId, code, "Requirement " + code, null, mandatory, Clock.UtcNow));

            if (!active)
            {
                ResultAssert.Succeeded(requirement.Deactivate(Clock.UtcNow));
            }

            Requirements.Items.Add(requirement);
            return requirement;
        }

        // A draft application created through the real handler (so it follows the same rules as the API).
        public async Task<ApplicationDetail> CreateDraftAsync(Guid applicantId, Guid programId)
        {
            var result = await ApplicationHandler.CreateAsync(new CreateApplicationCommand(applicantId, programId, YearId), Request);
            return ResultAssert.Succeeded(result);
        }
    }
}
