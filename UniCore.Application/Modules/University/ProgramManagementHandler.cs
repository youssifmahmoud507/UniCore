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
    public sealed class ProgramManagementHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IRepository<AcademicProgram> programs, IRepository<Department> departments, IProgramUsageChecker usage,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<AcademicProgram> _programs = programs;
        private readonly IRepository<Department> _departments = departments;
        private readonly IProgramUsageChecker _usage = usage;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task AuditAsync(string action, AcademicProgram program, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken)
            => _audit.WriteAsync(
                new AuditEntry(action, _user.UserId, "AcademicProgram", program.Id.ToString(),
                    request.IpAddress, request.UserAgent, oldValues, newValues),
                cancellationToken);

        private static string Describe(AcademicProgram p) => $"Code={p.Code};Name={p.Name};Degree={p.DegreeType};Years={p.DurationYears};Credits={p.TotalRequiredCredits};IsActive={p.IsActive}";

        // The program's scope is its department (and the faculty above it, through IScopeHierarchy).
        private Task<Result> RequireManageInDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.University.Manage, ResourceContext.InScope(ScopeType.Department, departmentId), cancellationToken);

        // Changing an existing program: first the caller must hold the permission at all, then (once we know
        // which department the program is in) the scope must cover that department. Doing it in two steps means
        // a caller without the permission cannot learn whether a program id exists.
        private async Task<Result<AcademicProgram>> LoadForManageAsync(Guid programId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.University.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<AcademicProgram>.Fail(permission.Errors);
            }

            var program = await _programs.GetByIdAsync(programId, cancellationToken);
            if (program is null)
            {
                return UniversityErrors.ProgramNotFound;
            }

            var scope = await RequireManageInDepartmentAsync(program.DepartmentId, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<AcademicProgram>.Fail(scope.Errors);
            }

            return program;
        }

        // ---------- queries ----------

        public async Task<Result<PagedResult<ProgramSummary>>> ListAsync(PaginationQuery query, string? search, string? sort, bool? isActive, Guid? departmentId, DegreeType? degreeType, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<ProgramSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, ProgramsListSpecification.SortFields, "code");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<ProgramSummary>>.Fail(parsed.Errors);
            }

            var specification = new ProgramsListSpecification(query, ListQueryRules.NormalizeSearch(search), isActive, departmentId, degreeType, sortOption);

            var items = await _programs.ListAsync(specification, cancellationToken);
            var total = await _programs.CountAsync(specification, cancellationToken);

            var summaries = items.Select(p => UniversityMapper.ToSummary(p)).ToList();

            return Result<PagedResult<ProgramSummary>>.Ok(PagedResult<ProgramSummary>.Create(summaries, query, total));
        }

        public async Task<Result<ProgramSummary>> GetAsync(Guid programId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<ProgramSummary>.Fail(auth.Errors);
            }

            var program = await _programs.GetByIdAsync(programId, cancellationToken);

            return program is null ? UniversityErrors.ProgramNotFound : UniversityMapper.ToSummary(program);
        }

        // ---------- changes ----------

        public async Task<Result<ProgramSummary>> CreateAsync(CreateProgramCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireManageInDepartmentAsync(command.DepartmentId, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<ProgramSummary>.Fail(auth.Errors);
            }

            if (command.DepartmentId == Guid.Empty)
            {
                return UniversityErrors.InvalidParent;
            }

            // A missing degree type is a validation error, not "Diploma" (the first enum value).
            if (command.DegreeType is not { } degreeType)
            {
                return UniversityErrors.InvalidDegreeType;
            }

            var parent = await _departments.GetByIdAsync(command.DepartmentId, cancellationToken);
            if (parent is null)
            {
                return UniversityErrors.DepartmentNotFound;
            }

            if (!parent.IsActive)
            {
                return UniversityErrors.ParentInactive;
            }

            var created = AcademicProgram.Create(command.DepartmentId, command.Code, command.Name, degreeType, command.DurationYears, command.TotalRequiredCredits, _clock.UtcNow);
            if (!created.TryGetValue(out var program))
            {
                return Result<ProgramSummary>.Fail(created.Errors);
            }

            _programs.Add(program);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<ProgramSummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.ProgramCreated, program, request, null, Describe(program), cancellationToken);

            return UniversityMapper.ToSummary(program);
        }

        public async Task<Result<ProgramSummary>> UpdateAsync(Guid programId, UpdateProgramCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(programId, cancellationToken);
            if (!loaded.TryGetValue(out var program))
            {
                return Result<ProgramSummary>.Fail(loaded.Errors);
            }

            if (command.DegreeType is not { } degreeType)
            {
                return UniversityErrors.InvalidDegreeType;
            }

            var before = Describe(program);

            var updated = program.UpdateDetails(command.Name, degreeType, command.DurationYears, command.TotalRequiredCredits, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<ProgramSummary>.Fail(updated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<ProgramSummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.ProgramUpdated, program, request, before, Describe(program), cancellationToken);

            return UniversityMapper.ToSummary(program);
        }

        public async Task<Result<ProgramSummary>> ActivateAsync(Guid programId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(programId, cancellationToken);
            if (!loaded.TryGetValue(out var program))
            {
                return Result<ProgramSummary>.Fail(loaded.Errors);
            }

            // A program of an inactive department stays inactive until the department is reactivated.
            var parent = await _departments.GetByIdAsync(program.DepartmentId, cancellationToken);
            if (parent is null || !parent.IsActive)
            {
                return UniversityErrors.ParentInactive;
            }

            var before = Describe(program);

            var activated = program.Activate(_clock.UtcNow);
            if (activated.IsFailure)
            {
                return Result<ProgramSummary>.Fail(activated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<ProgramSummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.ProgramActivated, program, request, before, Describe(program), cancellationToken);

            return UniversityMapper.ToSummary(program);
        }

        public async Task<Result<ProgramSummary>> DeactivateAsync(Guid programId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(programId, cancellationToken);
            if (!loaded.TryGetValue(out var program))
            {
                return Result<ProgramSummary>.Fail(loaded.Errors);
            }

            // A failed lookup must not look like "no students", so its error is returned as it is.
            var inUse = await _usage.HasActiveStudentsAsync(programId, cancellationToken);
            if (!inUse.TryGetValue(out var hasActiveStudents))
            {
                return Result<ProgramSummary>.Fail(inUse.Errors);
            }

            var before = Describe(program);

            var deactivated = program.Deactivate(hasActiveStudents, _clock.UtcNow);
            if (deactivated.IsFailure)
            {
                return Result<ProgramSummary>.Fail(deactivated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<ProgramSummary>.Fail(saved.Errors);
            }

            await AuditAsync(UniversityAuditActions.ProgramDeactivated, program, request, before, Describe(program), cancellationToken);

            return UniversityMapper.ToSummary(program);
        }
    }
}
