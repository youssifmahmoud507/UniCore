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
    public sealed class FacultyManagementHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IRepository<Faculty> faculties, IRepository<UniversityEntity> universities, IRepository<Department> departments,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        // Creating a faculty is a university-level action. Changing one can also be done by a user whose
        // scope covers that faculty (for example its dean, if the role was given University.Manage).
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<Faculty> _faculties = faculties;
        private readonly IRepository<UniversityEntity> _universities = universities;
        private readonly IRepository<Department> _departments = departments;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task AuditAsync(string action, Faculty faculty, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken)
            => _audit.WriteAsync(
                new AuditEntry(action, _user.UserId, "Faculty", faculty.Id.ToString(),
                    request.IpAddress, request.UserAgent, oldValues, newValues),
                cancellationToken);

        private static string Describe(Faculty f) => $"Code={f.Code};Name={f.Name};IsActive={f.IsActive}";

        private Task<Result> RequireManageAsync(Guid facultyId, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.University.Manage, ResourceContext.InScope(ScopeType.Faculty, facultyId), cancellationToken);

        // ---------- queries ----------

        public async Task<Result<PagedResult<FacultySummary>>> ListAsync(PaginationQuery query, string? search, string? sort, bool? isActive, Guid? universityId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<FacultySummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, FacultiesListSpecification.SortFields, "code");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<FacultySummary>>.Fail(parsed.Errors);
            }

            var specification = new FacultiesListSpecification(query, ListQueryRules.NormalizeSearch(search), isActive, universityId, sortOption);

            var items = await _faculties.ListAsync(specification, cancellationToken);
            var total = await _faculties.CountAsync(specification, cancellationToken);

            var summaries = items.Select(f => UniversityMapper.ToSummary(f)).ToList();

            return Result<PagedResult<FacultySummary>>.Ok(PagedResult<FacultySummary>.Create(summaries, query, total));
        }

        public async Task<Result<FacultySummary>> GetAsync(Guid facultyId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FacultySummary>.Fail(auth.Errors);
            }

            var faculty = await _faculties.GetByIdAsync(facultyId, cancellationToken);

            return faculty is null ? UniversityErrors.FacultyNotFound : UniversityMapper.ToSummary(faculty);
        }

        // ---------- changes ----------

        public async Task<Result<FacultySummary>> CreateAsync(CreateFacultyCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FacultySummary>.Fail(auth.Errors);
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

            var created = Faculty.Create(command.UniversityId, command.Code, command.Name, command.Description, command.Email, command.Phone, _clock.UtcNow);
            if (!created.TryGetValue(out var faculty))
            {
                return Result<FacultySummary>.Fail(created.Errors);
            }

            _faculties.Add(faculty);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FacultySummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.FacultyCreated, faculty, request, null, Describe(faculty), cancellationToken);

            return UniversityMapper.ToSummary(faculty);
        }

        public async Task<Result<FacultySummary>> UpdateAsync(Guid facultyId, UpdateFacultyCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireManageAsync(facultyId, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FacultySummary>.Fail(auth.Errors);
            }

            var faculty = await _faculties.GetByIdAsync(facultyId, cancellationToken);
            if (faculty is null)
            {
                return UniversityErrors.FacultyNotFound;
            }

            var before = Describe(faculty);

            var updated = faculty.UpdateDetails(command.Name, command.Description, command.Email, command.Phone, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<FacultySummary>.Fail(updated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FacultySummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.FacultyUpdated, faculty, request, before, Describe(faculty), cancellationToken);

            return UniversityMapper.ToSummary(faculty);
        }

        public async Task<Result<FacultySummary>> ActivateAsync(Guid facultyId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireManageAsync(facultyId, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FacultySummary>.Fail(auth.Errors);
            }

            var faculty = await _faculties.GetByIdAsync(facultyId, cancellationToken);
            if (faculty is null)
            {
                return UniversityErrors.FacultyNotFound;
            }

            // A faculty of an inactive university stays inactive until the university is reactivated.
            var parent = await _universities.GetByIdAsync(faculty.UniversityId, cancellationToken);
            if (parent is null || !parent.IsActive)
            {
                return UniversityErrors.ParentInactive;
            }

            var before = Describe(faculty);

            var activated = faculty.Activate(_clock.UtcNow);
            if (activated.IsFailure)
            {
                return Result<FacultySummary>.Fail(activated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FacultySummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.FacultyActivated, faculty, request, before, Describe(faculty), cancellationToken);

            return UniversityMapper.ToSummary(faculty);
        }

        public async Task<Result<FacultySummary>> DeactivateAsync(Guid facultyId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireManageAsync(facultyId, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FacultySummary>.Fail(auth.Errors);
            }

            var faculty = await _faculties.GetByIdAsync(facultyId, cancellationToken);
            if (faculty is null)
            {
                return UniversityErrors.FacultyNotFound;
            }

            var hasDepartments = await _departments.AnyAsync(new ActiveDepartmentsOfFacultySpecification(facultyId), cancellationToken);

            var before = Describe(faculty);

            var deactivated = faculty.Deactivate(hasDepartments, _clock.UtcNow);
            if (deactivated.IsFailure)
            {
                return Result<FacultySummary>.Fail(deactivated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FacultySummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.FacultyDeactivated, faculty, request, before, Describe(faculty), cancellationToken);

            return UniversityMapper.ToSummary(faculty);
        }
    }
}
