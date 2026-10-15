using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.University.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Common.ValueObjects;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;

// The namespace UniCore.Application.Modules.University hides the class name, so the entity gets an alias.
using UniversityEntity = UniCore.Domain.Modules.University.University;

namespace UniCore.Application.Modules.University
{
    public sealed class CampusManagementHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IRepository<Campus> campuses, IRepository<UniversityEntity> universities, IRepository<Building> buildings,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        // A campus belongs to the whole university, so it is managed at the top level only.
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<Campus> _campuses = campuses;
        private readonly IRepository<UniversityEntity> _universities = universities;
        private readonly IRepository<Building> _buildings = buildings;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task AuditAsync(string action, Campus campus, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken)
            => _audit.WriteAsync(
                new AuditEntry(action, _user.UserId, "Campus", campus.Id.ToString(),
                    request.IpAddress, request.UserAgent, oldValues, newValues),
                cancellationToken);

        private static string Describe(Campus c) => $"Code={c.Code};Name={c.Name};TimeZone={c.TimeZone};IsActive={c.IsActive}";

        // The domain checks the format of the time zone id; here we check that the server knows it.
        private static bool IsUnknownTimeZone(string? timeZone)
        {
            var clean = timeZone?.Trim();
            return FieldRules.IsValidTimeZoneId(clean) && !TimeZoneRules.Exists(clean);
        }

        // ---------- queries ----------

        public async Task<Result<PagedResult<CampusSummary>>> ListAsync(PaginationQuery query, string? search, string? sort, bool? isActive, Guid? universityId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<CampusSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, CampusesListSpecification.SortFields, "code");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<CampusSummary>>.Fail(parsed.Errors);
            }

            var specification = new CampusesListSpecification(query, ListQueryRules.NormalizeSearch(search), isActive, universityId, sortOption);

            var items = await _campuses.ListAsync(specification, cancellationToken);
            var total = await _campuses.CountAsync(specification, cancellationToken);

            var summaries = items.Select(c => UniversityMapper.ToSummary(c)).ToList();

            return Result<PagedResult<CampusSummary>>.Ok(PagedResult<CampusSummary>.Create(summaries, query, total));
        }

        public async Task<Result<CampusSummary>> GetAsync(Guid campusId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<CampusSummary>.Fail(auth.Errors);
            }

            var campus = await _campuses.GetByIdAsync(campusId, cancellationToken);

            return campus is null ? UniversityErrors.CampusNotFound : UniversityMapper.ToSummary(campus);
        }

        // ---------- changes ----------

        public async Task<Result<CampusSummary>> CreateAsync(CreateCampusCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<CampusSummary>.Fail(auth.Errors);
            }

            if (command.UniversityId == Guid.Empty)
            {
                return UniversityErrors.InvalidParent;
            }

            var parent = await _universities.GetByIdAsync(command.UniversityId, cancellationToken);
            if (parent is null)
            {
                return UniversityErrors.UniversityNotFound;
            }

            if (!parent.IsActive)
            {
                return UniversityErrors.ParentInactive;
            }

            var address = BuildAddress(command.Address);
            if (!address.TryGetValue(out var addressValue))
            {
                return Result<CampusSummary>.Fail(address.Errors);
            }

            if (IsUnknownTimeZone(command.TimeZone))
            {
                return UniversityErrors.TimeZoneUnknown;
            }

            var created = Campus.Create(command.UniversityId, command.Code, command.Name, addressValue, command.TimeZone);
            if (!created.TryGetValue(out var campus))
            {
                return Result<CampusSummary>.Fail(created.Errors);
            }

            _campuses.Add(campus);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<CampusSummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.CampusCreated, campus, request, null, Describe(campus), cancellationToken);

            return UniversityMapper.ToSummary(campus);
        }

        public async Task<Result<CampusSummary>> UpdateAsync(Guid campusId, UpdateCampusCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<CampusSummary>.Fail(auth.Errors);
            }

            var campus = await _campuses.GetByIdAsync(campusId, cancellationToken);
            if (campus is null)
            {
                return UniversityErrors.CampusNotFound;
            }

            var address = BuildAddress(command.Address);
            if (!address.TryGetValue(out var addressValue))
            {
                return Result<CampusSummary>.Fail(address.Errors);
            }

            // Checked before the entity is touched, so a rejected update never leaves it half changed.
            if (IsUnknownTimeZone(command.TimeZone))
            {
                return UniversityErrors.TimeZoneUnknown;
            }

            var before = Describe(campus);

            var updated = campus.Update(command.Name, addressValue, command.TimeZone);
            if (updated.IsFailure)
            {
                return Result<CampusSummary>.Fail(updated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<CampusSummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.CampusUpdated, campus, request, before, Describe(campus), cancellationToken);

            return UniversityMapper.ToSummary(campus);
        }

        public async Task<Result<CampusSummary>> ActivateAsync(Guid campusId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<CampusSummary>.Fail(auth.Errors);
            }

            var campus = await _campuses.GetByIdAsync(campusId, cancellationToken);
            if (campus is null)
            {
                return UniversityErrors.CampusNotFound;
            }

            // A campus of an inactive university stays inactive until the university is reactivated.
            var parent = await _universities.GetByIdAsync(campus.UniversityId, cancellationToken);
            if (parent is null || !parent.IsActive)
            {
                return UniversityErrors.ParentInactive;
            }

            var before = Describe(campus);

            var activated = campus.Activate();
            if (activated.IsFailure)
            {
                return Result<CampusSummary>.Fail(activated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<CampusSummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.CampusActivated, campus, request, before, Describe(campus), cancellationToken);

            return UniversityMapper.ToSummary(campus);
        }

        public async Task<Result<CampusSummary>> DeactivateAsync(Guid campusId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<CampusSummary>.Fail(auth.Errors);
            }

            var campus = await _campuses.GetByIdAsync(campusId, cancellationToken);
            if (campus is null)
            {
                return UniversityErrors.CampusNotFound;
            }

            var hasBuildings = await _buildings.AnyAsync(new ActiveBuildingsOfCampusSpecification(campusId), cancellationToken);

            var before = Describe(campus);

            var deactivated = campus.Deactivate(hasBuildings);
            if (deactivated.IsFailure)
            {
                return Result<CampusSummary>.Fail(deactivated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<CampusSummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.CampusDeactivated, campus, request, before, Describe(campus), cancellationToken);

            return UniversityMapper.ToSummary(campus);
        }

        private static Result<Address> BuildAddress(AddressInput? input)
        {
            if (input is null)
            {
                return UniversityErrors.AddressRequired;
            }

            return Address.Create(input.Street, input.City, input.State, input.PostalCode, input.Country);
        }
    }
}
