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
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.People
{
    // Teaching assistants (an employee, a graduate student, or both). The scope is the employee's department,
    // or the top level when the assistant is a student only.
    public sealed class TeachingAssistantHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IRepository<TeachingAssistant> assistants, IRepository<Employee> employees, IRepository<Student> students,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<TeachingAssistant> _assistants = assistants;
        private readonly IRepository<Employee> _employees = employees;
        private readonly IRepository<Student> _students = students;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(TeachingAssistant t) => $"Employee={t.EmployeeId};Student={t.StudentId};IsActive={t.IsActive}";

        private async Task<Result<TeachingAssistantSummary>> CommitAsync(TeachingAssistant assistant, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "TeachingAssistant", assistant.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(assistant));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
            return committed.IsFailure ? Result<TeachingAssistantSummary>.Fail(committed.Errors) : PeopleMapper.ToSummary(assistant);
        }

        private Task<Result> AuthorizeInScopeAsync(Employee? employee, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.People.Manage, employee?.DepartmentId is { } id ? ResourceContext.InScope(ScopeType.Department, id) : GlobalResource, cancellationToken);

        private async Task<Result<TeachingAssistant>> LoadForManageAsync(Guid assistantId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.People.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<TeachingAssistant>.Fail(permission.Errors);
            }

            var assistant = await _assistants.GetByIdAsync(assistantId, cancellationToken);
            if (assistant is null)
            {
                return PeopleErrors.TeachingAssistantNotFound;
            }

            var employee = assistant.EmployeeId is { } id ? await _employees.GetByIdAsync(id, cancellationToken) : null;
            var scope = await AuthorizeInScopeAsync(employee, cancellationToken);
            return scope.IsFailure ? Result<TeachingAssistant>.Fail(scope.Errors) : assistant;
        }

        public async Task<Result<PagedResult<TeachingAssistantSummary>>> ListAsync(PaginationQuery query, string? sort, bool? isActive, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.People.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<TeachingAssistantSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, TeachingAssistantsListSpecification.SortFields, "createdAt");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<TeachingAssistantSummary>>.Fail(parsed.Errors);
            }

            var specification = new TeachingAssistantsListSpecification(query, isActive, sortOption);
            var items = await _assistants.ListAsync(specification, cancellationToken);
            var total = await _assistants.CountAsync(specification, cancellationToken);

            return Result<PagedResult<TeachingAssistantSummary>>.Ok(PagedResult<TeachingAssistantSummary>.Create(items.Select(t => PeopleMapper.ToSummary(t)).ToList(), query, total));
        }

        public async Task<Result<TeachingAssistantSummary>> GetAsync(Guid assistantId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.People.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<TeachingAssistantSummary>.Fail(auth.Errors);
            }

            var assistant = await _assistants.GetByIdAsync(assistantId, cancellationToken);
            return assistant is null ? PeopleErrors.TeachingAssistantNotFound : PeopleMapper.ToSummary(assistant);
        }

        public async Task<Result<TeachingAssistantSummary>> CreateAsync(CreateTeachingAssistantCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.People.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<TeachingAssistantSummary>.Fail(permission.Errors);
            }

            // The entity rule first: at least an employee or a student.
            var created = TeachingAssistant.Create(command.EmployeeId, command.StudentId, _clock.UtcNow);
            if (!created.TryGetValue(out var assistant))
            {
                return Result<TeachingAssistantSummary>.Fail(created.Errors);
            }

            Employee? employee = null;
            if (assistant.EmployeeId is { } employeeId)
            {
                employee = await _employees.GetByIdAsync(employeeId, cancellationToken);
                if (employee is null)
                {
                    return PeopleErrors.EmployeeNotFound;
                }
            }

            var scope = await AuthorizeInScopeAsync(employee, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<TeachingAssistantSummary>.Fail(scope.Errors);
            }

            if (employee is not null && employee.Status != EmployeeStatus.Active)
            {
                return PeopleErrors.EmployeeNotActive;
            }

            if (assistant.StudentId is { } studentId && await _students.GetByIdAsync(studentId, cancellationToken) is null)
            {
                return PeopleErrors.StudentNotFound;
            }

            if (employee is not null && await _assistants.AnyAsync(new TeachingAssistantByEmployeeSpecification(employee.Id), cancellationToken))
            {
                return PeopleErrors.TeachingAssistantExists;
            }

            if (assistant.StudentId is { } sid && await _assistants.AnyAsync(new TeachingAssistantByStudentSpecification(sid), cancellationToken))
            {
                return PeopleErrors.TeachingAssistantExists;
            }

            _assistants.Add(assistant);
            return await CommitAsync(assistant, PeopleAuditActions.TeachingAssistantCreated, null, request, cancellationToken);
        }

        public async Task<Result<TeachingAssistantSummary>> ActivateAsync(Guid assistantId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(assistantId, cancellationToken);
            if (!loaded.TryGetValue(out var assistant))
            {
                return Result<TeachingAssistantSummary>.Fail(loaded.Errors);
            }

            var before = Describe(assistant);
            var result = assistant.Activate(_clock.UtcNow);
            if (result.IsFailure)
            {
                return Result<TeachingAssistantSummary>.Fail(result.Errors);
            }

            return await CommitAsync(assistant, PeopleAuditActions.TeachingAssistantActivated, before, request, cancellationToken);
        }

        public async Task<Result<TeachingAssistantSummary>> DeactivateAsync(Guid assistantId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(assistantId, cancellationToken);
            if (!loaded.TryGetValue(out var assistant))
            {
                return Result<TeachingAssistantSummary>.Fail(loaded.Errors);
            }

            var before = Describe(assistant);
            var result = assistant.Deactivate(_clock.UtcNow);
            if (result.IsFailure)
            {
                return Result<TeachingAssistantSummary>.Fail(result.Errors);
            }

            return await CommitAsync(assistant, PeopleAuditActions.TeachingAssistantDeactivated, before, request, cancellationToken);
        }
    }
}
