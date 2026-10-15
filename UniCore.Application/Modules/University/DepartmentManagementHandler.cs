using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.University.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University
{
    public sealed class DepartmentManagementHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IRepository<Department> departments, IRepository<Faculty> faculties, IRepository<AcademicProgram> programs,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<Department> _departments = departments;
        private readonly IRepository<Faculty> _faculties = faculties;
        private readonly IRepository<AcademicProgram> _programs = programs;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task AuditAsync(string action, Department department, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken)
            => _audit.WriteAsync(
                new AuditEntry(action, _user.UserId, "Department", department.Id.ToString(),
                    request.IpAddress, request.UserAgent, oldValues, newValues),
                cancellationToken);

        private static string Describe(Department d) => $"Code={d.Code};Name={d.Name};IsActive={d.IsActive}";

        // Creating needs a scope that covers the faculty; changing needs one that covers the department.
        // A user with Global scope passes both. The department's chain (Department -> Faculty) comes from
        // IScopeHierarchy, so a dean of the faculty is covered too.
        private Task<Result> RequireManageInFacultyAsync(Guid facultyId, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.University.Manage, ResourceContext.InScope(ScopeType.Faculty, facultyId), cancellationToken);

        private Task<Result> RequireManageDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.University.Manage, ResourceContext.InScope(ScopeType.Department, departmentId), cancellationToken);

        // ---------- queries ----------

        public async Task<Result<PagedResult<DepartmentSummary>>> ListAsync(PaginationQuery query, string? search, string? sort, bool? isActive, Guid? facultyId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<DepartmentSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, DepartmentsListSpecification.SortFields, "code");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<DepartmentSummary>>.Fail(parsed.Errors);
            }

            var specification = new DepartmentsListSpecification(query, ListQueryRules.NormalizeSearch(search), isActive, facultyId, sortOption);

            var items = await _departments.ListAsync(specification, cancellationToken);
            var total = await _departments.CountAsync(specification, cancellationToken);

            var summaries = items.Select(d => UniversityMapper.ToSummary(d)).ToList();

            return Result<PagedResult<DepartmentSummary>>.Ok(PagedResult<DepartmentSummary>.Create(summaries, query, total));
        }

        public async Task<Result<DepartmentSummary>> GetAsync(Guid departmentId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<DepartmentSummary>.Fail(auth.Errors);
            }

            var department = await _departments.GetByIdAsync(departmentId, cancellationToken);

            return department is null ? UniversityErrors.DepartmentNotFound : UniversityMapper.ToSummary(department);
        }

        // ---------- changes ----------

        public async Task<Result<DepartmentSummary>> CreateAsync(CreateDepartmentCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireManageInFacultyAsync(command.FacultyId, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<DepartmentSummary>.Fail(auth.Errors);
            }

            if (command.FacultyId == Guid.Empty)
            {
                return UniversityErrors.InvalidParent;
            }

            var parent = await _faculties.GetByIdAsync(command.FacultyId, cancellationToken);
            if (parent is null)
            {
                return UniversityErrors.FacultyNotFound;
            }

            if (!parent.IsActive)
            {
                return UniversityErrors.ParentInactive;
            }

            var created = Department.Create(command.FacultyId, command.Code, command.Name, command.Description, command.Email, command.Phone);
            if (!created.TryGetValue(out var department))
            {
                return Result<DepartmentSummary>.Fail(created.Errors);
            }

            _departments.Add(department);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<DepartmentSummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.DepartmentCreated, department, request, null, Describe(department), cancellationToken);

            return UniversityMapper.ToSummary(department);
        }

        public async Task<Result<DepartmentSummary>> UpdateAsync(Guid departmentId, UpdateDepartmentCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireManageDepartmentAsync(departmentId, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<DepartmentSummary>.Fail(auth.Errors);
            }

            var department = await _departments.GetByIdAsync(departmentId, cancellationToken);
            if (department is null)
            {
                return UniversityErrors.DepartmentNotFound;
            }

            var before = Describe(department);

            var updated = department.UpdateDetails(command.Name, command.Description, command.Email, command.Phone);
            if (updated.IsFailure)
            {
                return Result<DepartmentSummary>.Fail(updated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<DepartmentSummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.DepartmentUpdated, department, request, before, Describe(department), cancellationToken);

            return UniversityMapper.ToSummary(department);
        }

        public async Task<Result<DepartmentSummary>> ActivateAsync(Guid departmentId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireManageDepartmentAsync(departmentId, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<DepartmentSummary>.Fail(auth.Errors);
            }

            var department = await _departments.GetByIdAsync(departmentId, cancellationToken);
            if (department is null)
            {
                return UniversityErrors.DepartmentNotFound;
            }

            // A department of an inactive faculty stays inactive until the faculty is reactivated.
            var parent = await _faculties.GetByIdAsync(department.FacultyId, cancellationToken);
            if (parent is null || !parent.IsActive)
            {
                return UniversityErrors.ParentInactive;
            }

            var before = Describe(department);

            var activated = department.Activate();
            if (activated.IsFailure)
            {
                return Result<DepartmentSummary>.Fail(activated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<DepartmentSummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.DepartmentActivated, department, request, before, Describe(department), cancellationToken);

            return UniversityMapper.ToSummary(department);
        }

        public async Task<Result<DepartmentSummary>> DeactivateAsync(Guid departmentId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireManageDepartmentAsync(departmentId, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<DepartmentSummary>.Fail(auth.Errors);
            }

            var department = await _departments.GetByIdAsync(departmentId, cancellationToken);
            if (department is null)
            {
                return UniversityErrors.DepartmentNotFound;
            }

            var hasPrograms = await _programs.AnyAsync(new ActiveProgramsOfDepartmentSpecification(departmentId), cancellationToken);

            var before = Describe(department);

            var deactivated = department.Deactivate(hasPrograms);
            if (deactivated.IsFailure)
            {
                return Result<DepartmentSummary>.Fail(deactivated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<DepartmentSummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.DepartmentDeactivated, department, request, before, Describe(department), cancellationToken);

            return UniversityMapper.ToSummary(department);
        }
    }
}
