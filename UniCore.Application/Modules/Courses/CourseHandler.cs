using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Modules.Courses.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Courses
{
    // Courses. Reading needs Courses.Read. Changing needs Courses.Manage inside the course's department
    // (and the faculty above it, through IScopeHierarchy).
    public sealed class CourseHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IAggregateRepository<Course> courses, IAggregateRepository<CourseVersion> versions, IRepository<Department> departments,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Course> _courses = courses;
        private readonly IAggregateRepository<CourseVersion> _versions = versions;
        private readonly IRepository<Department> _departments = departments;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(Course c) => $"Department={c.DepartmentId};Code={c.Code};Title={c.Title};Type={c.CourseType};IsActive={c.IsActive}";

        private async Task<Result<CourseSummary>> CommitAsync(Course course, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "Course", course.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(course));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<CourseSummary>.Fail(committed.Errors) : CourseMapper.ToSummary(course);
        }

        // Permission first, then the department, then the scope. A caller without the permission
        // cannot learn whether a department or course id exists.
        private async Task<Result<Department>> AuthorizeForDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.Courses.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<Department>.Fail(permission.Errors);
            }

            var department = departmentId == Guid.Empty ? null : await _departments.GetByIdAsync(departmentId, cancellationToken);
            if (department is null)
            {
                return CoursesErrors.DepartmentNotFound;
            }

            var scope = await _authorizer.AuthorizeAsync(Permissions.Courses.Manage, ResourceContext.InScope(ScopeType.Department, department.Id), cancellationToken);

            return scope.IsFailure ? Result<Department>.Fail(scope.Errors) : department;
        }

        private async Task<Result<Course>> LoadForManageAsync(Guid courseId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.Courses.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<Course>.Fail(permission.Errors);
            }

            var course = await _courses.GetByIdAsync(courseId, cancellationToken);
            if (course is null)
            {
                return CoursesErrors.CourseNotFound;
            }

            var scope = await _authorizer.AuthorizeAsync(Permissions.Courses.Manage, ResourceContext.InScope(ScopeType.Department, course.DepartmentId), cancellationToken);

            return scope.IsFailure ? Result<Course>.Fail(scope.Errors) : course;
        }

        // ---------- queries ----------

        public async Task<Result<PagedResult<CourseSummary>>> ListAsync(PaginationQuery query, string? search, string? sort, bool? isActive, Guid? departmentId, CourseType? courseType, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Courses.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<CourseSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, CoursesListSpecification.SortFields, "code");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<CourseSummary>>.Fail(parsed.Errors);
            }

            var specification = new CoursesListSpecification(query, ListQueryRules.NormalizeSearch(search), isActive, departmentId, courseType, sortOption);

            var items = await _courses.ListAsync(specification, cancellationToken);
            var total = await _courses.CountAsync(specification, cancellationToken);

            return Result<PagedResult<CourseSummary>>.Ok(PagedResult<CourseSummary>.Create([.. items.Select(c => CourseMapper.ToSummary(c))], query, total));
        }

        public async Task<Result<CourseSummary>> GetAsync(Guid courseId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Courses.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<CourseSummary>.Fail(auth.Errors);
            }

            var course = await _courses.GetByIdAsync(courseId, cancellationToken);

            return course is null ? CoursesErrors.CourseNotFound : CourseMapper.ToSummary(course);
        }

        // ---------- changes ----------

        public async Task<Result<CourseSummary>> CreateAsync(CreateCourseCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var authorized = await AuthorizeForDepartmentAsync(command.DepartmentId, cancellationToken);
            if (!authorized.TryGetValue(out var department))
            {
                return Result<CourseSummary>.Fail(authorized.Errors);
            }

            if (!department.IsActive)
            {
                return CoursesErrors.DepartmentInactive;
            }

            var created = Course.Create(department.Id, command.Code, command.Title, command.Description, command.CourseType, _clock.UtcNow);
            if (!created.TryGetValue(out var course))
            {
                return Result<CourseSummary>.Fail(created.Errors);
            }

            if (await _courses.AnyAsync(new CourseByCodeSpecification(department.Id, course.Code), cancellationToken))
            {
                return CoursesErrors.CourseCodeExists;
            }

            _courses.Add(course);

            return await CommitAsync(course, CoursesAuditActions.CourseCreated, null, request, cancellationToken);
        }

        public async Task<Result<CourseSummary>> UpdateAsync(Guid courseId, UpdateCourseCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(courseId, cancellationToken);
            if (!loaded.TryGetValue(out var course))
            {
                return Result<CourseSummary>.Fail(loaded.Errors);
            }

            var before = Describe(course);

            var updated = course.UpdateDetails(command.Title, command.Description, command.CourseType, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<CourseSummary>.Fail(updated.Errors);
            }

            return await CommitAsync(course, CoursesAuditActions.CourseUpdated, before, request, cancellationToken);
        }

        public async Task<Result<CourseSummary>> ActivateAsync(Guid courseId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(courseId, cancellationToken);
            if (!loaded.TryGetValue(out var course))
            {
                return Result<CourseSummary>.Fail(loaded.Errors);
            }

            // A course of an inactive department stays inactive until the department is reactivated.
            var department = await _departments.GetByIdAsync(course.DepartmentId, cancellationToken);
            if (department is null || !department.IsActive)
            {
                return CoursesErrors.DepartmentInactive;
            }

            var before = Describe(course);

            var activated = course.Activate(_clock.UtcNow);
            if (activated.IsFailure)
            {
                return Result<CourseSummary>.Fail(activated.Errors);
            }

            return await CommitAsync(course, CoursesAuditActions.CourseActivated, before, request, cancellationToken);
        }

        public async Task<Result<CourseSummary>> DeactivateAsync(Guid courseId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(courseId, cancellationToken);
            if (!loaded.TryGetValue(out var course))
            {
                return Result<CourseSummary>.Fail(loaded.Errors);
            }

            var hasActiveVersion = await _versions.AnyAsync(new CourseVersionByStatusSpecification(course.Id, CourseVersionStatus.Active), cancellationToken);

            var before = Describe(course);

            var deactivated = course.Deactivate(hasActiveVersion, _clock.UtcNow);
            if (deactivated.IsFailure)
            {
                return Result<CourseSummary>.Fail(deactivated.Errors);
            }

            return await CommitAsync(course, CoursesAuditActions.CourseDeactivated, before, request, cancellationToken);
        }
    }
}
