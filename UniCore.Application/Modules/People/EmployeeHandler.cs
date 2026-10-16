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
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.People
{
    // Employees. An employee with a department is managed inside that department's scope (and the faculty above it);
    // an employee without one (administrative staff) needs the top-level scope.
    public sealed class EmployeeHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IRepository<Employee> employees, IRepository<PersonProfile> persons, IRepository<Department> departments,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<Employee> _employees = employees;
        private readonly IRepository<PersonProfile> _persons = persons;
        private readonly IRepository<Department> _departments = departments;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(Employee e) => $"Number={e.EmployeeNumber};Person={e.PersonProfileId};Department={e.DepartmentId};JobTitle={e.JobTitle};Hired={e.HireDate:yyyy-MM-dd};Terminated={e.TerminationDate:yyyy-MM-dd};Status={e.Status}";

        private Task<Result> AuthorizeManageAsync(Guid? departmentId, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.People.Manage, departmentId is { } id && id != Guid.Empty ? ResourceContext.InScope(ScopeType.Department, id) : GlobalResource, cancellationToken);

        private async Task<Result<EmployeeSummary>> CommitAsync(Employee employee, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "Employee", employee.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(employee));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
            return committed.IsFailure ? Result<EmployeeSummary>.Fail(committed.Errors) : PeopleMapper.ToSummary(employee);
        }

        // Permission first, then the employee, then the scope of the employee's department.
        private async Task<Result<Employee>> LoadForManageAsync(Guid employeeId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.People.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<Employee>.Fail(permission.Errors);
            }

            var employee = await _employees.GetByIdAsync(employeeId, cancellationToken);
            if (employee is null)
            {
                return PeopleErrors.EmployeeNotFound;
            }

            var scope = await AuthorizeManageAsync(employee.DepartmentId, cancellationToken);
            return scope.IsFailure ? Result<Employee>.Fail(scope.Errors) : employee;
        }

        // The department must exist and be active.
        private async Task<Result> CheckDepartmentAsync(Guid? departmentId, CancellationToken cancellationToken)
        {
            if (departmentId is not { } id)
            {
                return Result.Ok();
            }

            var department = await _departments.GetByIdAsync(id, cancellationToken);
            if (department is null)
            {
                return Result.Fail(UniversityErrors.DepartmentNotFound);
            }

            return department.IsActive ? Result.Ok() : Result.Fail(UniversityErrors.ParentInactive);
        }

        public async Task<Result<PagedResult<EmployeeSummary>>> ListAsync(PaginationQuery query, string? search, string? sort, EmployeeStatus? status, Guid? departmentId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.People.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<EmployeeSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, EmployeesListSpecification.SortFields, "employeeNumber");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<EmployeeSummary>>.Fail(parsed.Errors);
            }

            var specification = new EmployeesListSpecification(query, ListQueryRules.NormalizeSearch(search), status, departmentId, sortOption);
            var items = await _employees.ListAsync(specification, cancellationToken);
            var total = await _employees.CountAsync(specification, cancellationToken);

            return Result<PagedResult<EmployeeSummary>>.Ok(PagedResult<EmployeeSummary>.Create(items.Select(e => PeopleMapper.ToSummary(e)).ToList(), query, total));
        }

        public async Task<Result<EmployeeSummary>> GetAsync(Guid employeeId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.People.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<EmployeeSummary>.Fail(auth.Errors);
            }

            var employee = await _employees.GetByIdAsync(employeeId, cancellationToken);
            return employee is null ? PeopleErrors.EmployeeNotFound : PeopleMapper.ToSummary(employee);
        }

        public async Task<Result<EmployeeSummary>> CreateAsync(CreateEmployeeCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await AuthorizeManageAsync(command.DepartmentId, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<EmployeeSummary>.Fail(auth.Errors);
            }

            var created = Employee.Create(command.PersonProfileId, command.EmployeeNumber, command.DepartmentId, command.JobTitle, command.HireDate, _clock.UtcNow);
            if (!created.TryGetValue(out var employee))
            {
                return Result<EmployeeSummary>.Fail(created.Errors);
            }

            if (await _persons.GetByIdAsync(employee.PersonProfileId, cancellationToken) is null)
            {
                return PeopleErrors.PersonNotFound;
            }

            var department = await CheckDepartmentAsync(employee.DepartmentId, cancellationToken);
            if (department.IsFailure)
            {
                return Result<EmployeeSummary>.Fail(department.Errors);
            }

            if (await _employees.AnyAsync(new EmployeeByPersonSpecification(employee.PersonProfileId), cancellationToken))
            {
                return PeopleErrors.EmployeeAlreadyExists;
            }

            if (await _employees.AnyAsync(new EmployeeByNumberSpecification(employee.EmployeeNumber), cancellationToken))
            {
                return PeopleErrors.EmployeeNumberExists;
            }

            _employees.Add(employee);
            return await CommitAsync(employee, PeopleAuditActions.EmployeeCreated, null, request, cancellationToken);
        }

        public async Task<Result<EmployeeSummary>> UpdateAsync(Guid employeeId, UpdateEmployeeCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(employeeId, cancellationToken);
            if (!loaded.TryGetValue(out var employee))
            {
                return Result<EmployeeSummary>.Fail(loaded.Errors);
            }

            // Moving an employee needs authority over the new department as well as the old one.
            if (command.DepartmentId != employee.DepartmentId)
            {
                var target = await AuthorizeManageAsync(command.DepartmentId, cancellationToken);
                if (target.IsFailure)
                {
                    return Result<EmployeeSummary>.Fail(target.Errors);
                }

                var department = await CheckDepartmentAsync(command.DepartmentId, cancellationToken);
                if (department.IsFailure)
                {
                    return Result<EmployeeSummary>.Fail(department.Errors);
                }
            }

            var before = Describe(employee);
            var updated = employee.Update(command.DepartmentId, command.JobTitle, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<EmployeeSummary>.Fail(updated.Errors);
            }

            return await CommitAsync(employee, PeopleAuditActions.EmployeeUpdated, before, request, cancellationToken);
        }

        // Ends the employment. The EmployeeTerminated event lets scheduling end the employee's open assignments.
        public async Task<Result<EmployeeSummary>> TerminateAsync(Guid employeeId, DateOnly terminationDate, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(employeeId, cancellationToken);
            if (!loaded.TryGetValue(out var employee))
            {
                return Result<EmployeeSummary>.Fail(loaded.Errors);
            }

            var before = Describe(employee);
            var terminated = employee.Terminate(terminationDate, _clock.UtcNow);
            if (terminated.IsFailure)
            {
                return Result<EmployeeSummary>.Fail(terminated.Errors);
            }

            return await CommitAsync(employee, PeopleAuditActions.EmployeeTerminated, before, request, cancellationToken);
        }
    }
}
