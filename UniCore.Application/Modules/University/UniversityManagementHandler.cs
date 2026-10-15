using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.University.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;

// The namespace UniCore.Application.Modules.University hides the class name, so the entity gets an alias.
using UniversityEntity = UniCore.Domain.Modules.University.University;

namespace UniCore.Application.Modules.University
{
    public sealed class UniversityManagementHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IRepository<UniversityEntity> universities, IRepository<Faculty> faculties, IRepository<Campus> campuses,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        // The university itself is managed at the top level only.
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<UniversityEntity> _universities = universities;
        private readonly IRepository<Faculty> _faculties = faculties;
        private readonly IRepository<Campus> _campuses = campuses;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task AuditAsync(string action, UniversityEntity university, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken)
            => _audit.WriteAsync(
                new AuditEntry(action, _user.UserId, "University", university.Id.ToString(),
                    request.IpAddress, request.UserAgent, oldValues, newValues),
                cancellationToken);

        private static string Describe(UniversityEntity u) => $"Code={u.Code};Name={u.Name};IsActive={u.IsActive}";

        // ---------- queries (reference data: the permission is enough) ----------

        public async Task<Result<PagedResult<UniversitySummary>>> ListAsync(PaginationQuery query, string? search, string? sort, bool? isActive, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<UniversitySummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, UniversitiesListSpecification.SortFields, "code");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<UniversitySummary>>.Fail(parsed.Errors);
            }

            var specification = new UniversitiesListSpecification(query, ListQueryRules.NormalizeSearch(search), isActive, sortOption);

            var items = await _universities.ListAsync(specification, cancellationToken);
            var total = await _universities.CountAsync(specification, cancellationToken);

            var summaries = items.Select(u => UniversityMapper.ToSummary(u)).ToList();

            return Result<PagedResult<UniversitySummary>>.Ok(PagedResult<UniversitySummary>.Create(summaries, query, total));
        }

        public async Task<Result<UniversitySummary>> GetAsync(Guid universityId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<UniversitySummary>.Fail(auth.Errors);
            }

            var university = await _universities.GetByIdAsync(universityId, cancellationToken);

            return university is null ? UniversityErrors.UniversityNotFound : UniversityMapper.ToSummary(university);
        }

        // ---------- changes ----------

        public async Task<Result<UniversitySummary>> CreateAsync(CreateUniversityCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<UniversitySummary>.Fail(auth.Errors);
            }

            var created = UniversityEntity.Create(command.Code, command.Name, command.Description, command.Email, command.Phone, command.Website, _clock.UtcNow);
            if (!created.TryGetValue(out var university))
            {
                return Result<UniversitySummary>.Fail(created.Errors);
            }

            _universities.Add(university);

            // A duplicate code or name is rejected by the unique indexes and comes back as DUPLICATE_RESOURCE.
            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<UniversitySummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.UniversityCreated, university, request, null, Describe(university), cancellationToken);

            return UniversityMapper.ToSummary(university);
        }

        public async Task<Result<UniversitySummary>> UpdateAsync(Guid universityId, UpdateUniversityCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<UniversitySummary>.Fail(auth.Errors);
            }

            var university = await _universities.GetByIdAsync(universityId, cancellationToken);
            if (university is null)
            {
                return UniversityErrors.UniversityNotFound;
            }

            var before = Describe(university);

            var updated = university.UpdateDetails(command.Name, command.Description, command.Email, command.Phone, command.Website, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<UniversitySummary>.Fail(updated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<UniversitySummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.UniversityUpdated, university, request, before, Describe(university), cancellationToken);

            return UniversityMapper.ToSummary(university);
        }

        public async Task<Result<UniversitySummary>> ActivateAsync(Guid universityId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<UniversitySummary>.Fail(auth.Errors);
            }

            var university = await _universities.GetByIdAsync(universityId, cancellationToken);
            if (university is null)
            {
                return UniversityErrors.UniversityNotFound;
            }

            var before = Describe(university);

            var activated = university.Activate(_clock.UtcNow);
            if (activated.IsFailure)
            {
                return Result<UniversitySummary>.Fail(activated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<UniversitySummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.UniversityActivated, university, request, before, Describe(university), cancellationToken);

            return UniversityMapper.ToSummary(university);
        }

        public async Task<Result<UniversitySummary>> DeactivateAsync(Guid universityId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<UniversitySummary>.Fail(auth.Errors);
            }

            var university = await _universities.GetByIdAsync(universityId, cancellationToken);
            if (university is null)
            {
                return UniversityErrors.UniversityNotFound;
            }

            // The aggregate does not hold its children, so the handler asks the database.
            var hasFaculties = await _faculties.AnyAsync(new ActiveFacultiesOfUniversitySpecification(universityId), cancellationToken);
            var hasCampuses = await _campuses.AnyAsync(new ActiveCampusesOfUniversitySpecification(universityId), cancellationToken);

            var before = Describe(university);

            var deactivated = university.Deactivate(hasFaculties || hasCampuses, _clock.UtcNow);
            if (deactivated.IsFailure)
            {
                return Result<UniversitySummary>.Fail(deactivated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<UniversitySummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.UniversityDeactivated, university, request, before, Describe(university), cancellationToken);

            return UniversityMapper.ToSummary(university);
        }
    }
}
