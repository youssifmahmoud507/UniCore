using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.People.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.People;

namespace UniCore.Application.Modules.People
{
    // Instructors. An instructor has no department of its own: the scope is the employee's department.
    public sealed class InstructorHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IRepository<Instructor> instructors, IRepository<Employee> employees,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<Instructor> _instructors = instructors;
        private readonly IRepository<Employee> _employees = employees;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(Instructor i) => $"Employee={i.EmployeeId};Rank={i.Rank};Specialization={i.Specialization};IsActive={i.IsActive}";

        private Task<Result> AuthorizeInScopeAsync(Employee employee, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.People.Manage, employee.DepartmentId is { } id ? ResourceContext.InScope(ScopeType.Department, id) : GlobalResource, cancellationToken);

        private async Task<Result<InstructorSummary>> CommitAsync(Instructor instructor, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "Instructor", instructor.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(instructor));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
            return committed.IsFailure ? Result<InstructorSummary>.Fail(committed.Errors) : PeopleMapper.ToSummary(instructor);
        }

        private async Task<Result<Instructor>> LoadForManageAsync(Guid instructorId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.People.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<Instructor>.Fail(permission.Errors);
            }

            var instructor = await _instructors.GetByIdAsync(instructorId, cancellationToken);
            if (instructor is null)
            {
                return PeopleErrors.InstructorNotFound;
            }

            var employee = await _employees.GetByIdAsync(instructor.EmployeeId, cancellationToken);
            if (employee is null)
            {
                return PeopleErrors.EmployeeNotFound;
            }

            var scope = await AuthorizeInScopeAsync(employee, cancellationToken);
            return scope.IsFailure ? Result<Instructor>.Fail(scope.Errors) : instructor;
        }

        public async Task<Result<PagedResult<InstructorSummary>>> ListAsync(PaginationQuery query, string? sort, bool? isActive, AcademicRank? rank, Guid? employeeId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.People.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<InstructorSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, InstructorsListSpecification.SortFields, "rank");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<InstructorSummary>>.Fail(parsed.Errors);
            }

            var specification = new InstructorsListSpecification(query, isActive, rank, employeeId, sortOption);
            var items = await _instructors.ListAsync(specification, cancellationToken);
            var total = await _instructors.CountAsync(specification, cancellationToken);

            return Result<PagedResult<InstructorSummary>>.Ok(PagedResult<InstructorSummary>.Create(items.Select(i => PeopleMapper.ToSummary(i)).ToList(), query, total));
        }

        public async Task<Result<InstructorSummary>> GetAsync(Guid instructorId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.People.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<InstructorSummary>.Fail(auth.Errors);
            }

            var instructor = await _instructors.GetByIdAsync(instructorId, cancellationToken);
            return instructor is null ? PeopleErrors.InstructorNotFound : PeopleMapper.ToSummary(instructor);
        }

        public async Task<Result<InstructorSummary>> CreateAsync(CreateInstructorCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.People.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<InstructorSummary>.Fail(permission.Errors);
            }

            if (command.EmployeeId == Guid.Empty)
            {
                return PeopleErrors.InvalidParent;
            }

            var employee = await _employees.GetByIdAsync(command.EmployeeId, cancellationToken);
            if (employee is null)
            {
                return PeopleErrors.EmployeeNotFound;
            }

            var scope = await AuthorizeInScopeAsync(employee, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<InstructorSummary>.Fail(scope.Errors);
            }

            if (command.Rank is not { } rank)
            {
                return PeopleErrors.InvalidAcademicRank;
            }

            if (employee.Status != EmployeeStatus.Active)
            {
                return PeopleErrors.EmployeeNotActive;
            }

            if (await _instructors.AnyAsync(new InstructorByEmployeeSpecification(employee.Id), cancellationToken))
            {
                return PeopleErrors.InstructorExists;
            }

            var created = Instructor.Create(employee.Id, rank, command.Specialization, _clock.UtcNow);
            if (!created.TryGetValue(out var instructor))
            {
                return Result<InstructorSummary>.Fail(created.Errors);
            }

            _instructors.Add(instructor);
            return await CommitAsync(instructor, PeopleAuditActions.InstructorCreated, null, request, cancellationToken);
        }

        public async Task<Result<InstructorSummary>> UpdateAsync(Guid instructorId, UpdateInstructorCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(instructorId, cancellationToken);
            if (!loaded.TryGetValue(out var instructor))
            {
                return Result<InstructorSummary>.Fail(loaded.Errors);
            }

            if (command.Rank is not { } rank)
            {
                return PeopleErrors.InvalidAcademicRank;
            }

            var before = Describe(instructor);
            var updated = instructor.Update(rank, command.Specialization, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<InstructorSummary>.Fail(updated.Errors);
            }

            return await CommitAsync(instructor, PeopleAuditActions.InstructorUpdated, before, request, cancellationToken);
        }

        public async Task<Result<InstructorSummary>> ActivateAsync(Guid instructorId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(instructorId, cancellationToken);
            if (!loaded.TryGetValue(out var instructor))
            {
                return Result<InstructorSummary>.Fail(loaded.Errors);
            }

            // A terminated employee cannot teach again until employed again.
            var employee = await _employees.GetByIdAsync(instructor.EmployeeId, cancellationToken);
            if (employee is null || employee.Status != EmployeeStatus.Active)
            {
                return PeopleErrors.EmployeeNotActive;
            }

            var before = Describe(instructor);
            var result = instructor.Activate(_clock.UtcNow);
            if (result.IsFailure)
            {
                return Result<InstructorSummary>.Fail(result.Errors);
            }

            return await CommitAsync(instructor, PeopleAuditActions.InstructorActivated, before, request, cancellationToken);
        }

        public async Task<Result<InstructorSummary>> DeactivateAsync(Guid instructorId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(instructorId, cancellationToken);
            if (!loaded.TryGetValue(out var instructor))
            {
                return Result<InstructorSummary>.Fail(loaded.Errors);
            }

            var before = Describe(instructor);
            var result = instructor.Deactivate(_clock.UtcNow);
            if (result.IsFailure)
            {
                return Result<InstructorSummary>.Fail(result.Errors);
            }

            return await CommitAsync(instructor, PeopleAuditActions.InstructorDeactivated, before, request, cancellationToken);
        }
    }
}
