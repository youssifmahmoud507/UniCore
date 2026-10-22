using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Courses.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Courses
{
    // Versions of a course. Reading needs Courses.Read; every change needs Courses.Manage inside the course's
    // department. Only a Draft can change: to change credits, hours or content, create a new draft from the
    // current version, edit it and activate it. Order of checks: the permission alone, then load, then the scope,
    // so a caller without the permission cannot learn whether an id exists.
    public sealed class CourseVersionHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IAggregateRepository<CourseVersion> versions, IAggregateRepository<Course> courses,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<CourseVersion> _versions = versions;
        private readonly IAggregateRepository<Course> _courses = courses;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private sealed record Loaded(CourseVersion Version, Course Course);

        private static string Describe(CourseVersion v)
            => $"Course={v.CourseId};Version={v.VersionNumber};Status={v.Status};Title={v.Title};Credits={v.Credits};Lecture={v.LectureHours};Practical={v.PracticalHours};From={v.EffectiveFrom:yyyy-MM-dd};To={v.EffectiveTo:yyyy-MM-dd}";

        private AuditEntry Entry(string action, CourseVersion v, RequestInfo request, string? before, string? after = null)
            => new(action, _user.UserId, "CourseVersion", v.Id.ToString(), request.IpAddress, request.UserAgent, before, after ?? Describe(v));

        private async Task<Result<CourseVersionSummary>> CommitAsync(CourseVersion version, AuditEntry entry, CancellationToken cancellationToken)
        {
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<CourseVersionSummary>.Fail(committed.Errors) : CourseMapper.ToSummary(version);
        }

        private async Task<Result<Course>> AuthorizeForCourseAsync(Guid courseId, CancellationToken cancellationToken)
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

        private async Task<Result<Loaded>> LoadForManageAsync(Guid versionId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.Courses.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<Loaded>.Fail(permission.Errors);
            }

            var version = await _versions.FirstOrDefaultAsync(new CourseVersionByIdSpecification(versionId), cancellationToken);
            if (version is null)
            {
                return CoursesErrors.VersionNotFound;
            }

            var course = await AuthorizeForCourseAsync(version.CourseId, cancellationToken);
            if (!course.TryGetValue(out var found))
            {
                return Result<Loaded>.Fail(course.Errors);
            }

            return new Loaded(version, found);
        }

        // ---------- queries ----------

        public async Task<Result<IReadOnlyList<CourseVersionSummary>>> ListAsync(Guid courseId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Courses.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<IReadOnlyList<CourseVersionSummary>>.Fail(auth.Errors);
            }

            if (await _courses.GetByIdAsync(courseId, cancellationToken) is null)
            {
                return CoursesErrors.CourseNotFound;
            }

            var items = await _versions.ListAsync(new VersionsOfCourseSpecification(courseId), cancellationToken);

            return Result<IReadOnlyList<CourseVersionSummary>>.Ok([.. items.Select(v => CourseMapper.ToSummary(v))]);
        }

        public async Task<Result<CourseVersionSummary>> GetAsync(Guid versionId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Courses.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<CourseVersionSummary>.Fail(auth.Errors);
            }

            var version = await _versions.GetByIdAsync(versionId, cancellationToken);

            return version is null ? CoursesErrors.VersionNotFound : CourseMapper.ToSummary(version);
        }

        // ---------- draft ----------

        public async Task<Result<CourseVersionSummary>> CreateAsync(Guid courseId, CreateCourseVersionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var authorized = await AuthorizeForCourseAsync(courseId, cancellationToken);
            if (!authorized.TryGetValue(out var course))
            {
                return Result<CourseVersionSummary>.Fail(authorized.Errors);
            }

            if (!course.IsActive)
            {
                return CoursesErrors.CourseInactive;
            }

            if (command.EffectiveFrom is not { } effectiveFrom)
            {
                return CoursesErrors.EffectiveDateInvalid;
            }

            if (await _versions.AnyAsync(new CourseVersionByStatusSpecification(courseId, CourseVersionStatus.Draft), cancellationToken))
            {
                return CoursesErrors.DraftExists;
            }

            var title = command.Title;
            var description = command.Description;
            var credits = command.Credits;
            var lectureHours = command.LectureHours;
            var practicalHours = command.PracticalHours;

            if (command.SourceVersionId is { } sourceId)
            {
                var source = await _versions.GetByIdAsync(sourceId, cancellationToken);
                if (source is null || source.CourseId != courseId)
                {
                    return CoursesErrors.VersionNotFound;
                }

                title ??= source.Title;
                description ??= source.Description;
                credits ??= source.Credits;
                lectureHours ??= source.LectureHours;
                practicalHours ??= source.PracticalHours;
            }

            var latest = (await _versions.ListAsync(new LatestVersionOfCourseSpecification(courseId), cancellationToken)).FirstOrDefault();
            var number = (latest?.VersionNumber ?? 0) + 1;

            var created = CourseVersion.Create(courseId, number, title, description, credits, lectureHours, practicalHours, effectiveFrom, _clock.UtcNow);
            if (!created.TryGetValue(out var version))
            {
                return Result<CourseVersionSummary>.Fail(created.Errors);
            }

            _versions.Add(version);

            var after = Describe(version) + (command.SourceVersionId is { } s ? $";CopiedFrom={s}" : string.Empty);

            return await CommitAsync(version, Entry(CoursesAuditActions.VersionCreated, version, request, null, after), cancellationToken);
        }

        public async Task<Result<CourseVersionSummary>> UpdateAsync(Guid versionId, UpdateCourseVersionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(versionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<CourseVersionSummary>.Fail(loaded.Errors);
            }

            if (command.EffectiveFrom is not { } effectiveFrom)
            {
                return CoursesErrors.EffectiveDateInvalid;
            }

            var before = Describe(context.Version);

            var updated = context.Version.UpdateDraft(command.Title, command.Description, command.Credits, command.LectureHours, command.PracticalHours, effectiveFrom, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<CourseVersionSummary>.Fail(updated.Errors);
            }

            return await CommitAsync(context.Version, Entry(CoursesAuditActions.VersionUpdated, context.Version, request, before), cancellationToken);
        }

        // ---------- lifecycle ----------

        // Draft -> Active. The version it replaces (the course's current Active one) is retired on the day before,
        // in the same save. The new version must start after the one it replaces and must not overlap an earlier
        // retired version.
        public async Task<Result<CourseVersionSummary>> ActivateAsync(Guid versionId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(versionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<CourseVersionSummary>.Fail(loaded.Errors);
            }

            var version = context.Version;

            if (version.Status != CourseVersionStatus.Draft)
            {
                return CoursesErrors.VersionInvalidState;
            }

            if (!context.Course.IsActive)
            {
                return CoursesErrors.CourseInactive;
            }

            var previous = await _versions.FirstOrDefaultAsync(new CourseVersionByStatusSpecification(version.CourseId, CourseVersionStatus.Active, tracking: true), cancellationToken);
            if (previous is not null && version.EffectiveFrom <= previous.EffectiveFrom)
            {
                return CoursesErrors.EffectiveDateInvalid;
            }

            if (await _versions.AnyAsync(new RetiredVersionEndingFromSpecification(version.CourseId, version.EffectiveFrom), cancellationToken))
            {
                return CoursesErrors.VersionOverlap;
            }

            var before = Describe(version);
            var now = _clock.UtcNow;

            var activated = version.Activate(now);
            if (activated.IsFailure)
            {
                return Result<CourseVersionSummary>.Fail(activated.Errors);
            }

            string? retiredBefore = null;

            if (previous is not null)
            {
                retiredBefore = Describe(previous);

                var retired = previous.Retire(version.EffectiveFrom.AddDays(-1), now);
                if (retired.IsFailure)
                {
                    return Result<CourseVersionSummary>.Fail(retired.Errors);
                }
            }

            var committed = await CommitAsync(version, Entry(CoursesAuditActions.VersionActivated, version, request, before), cancellationToken);

            if (committed.IsSuccess && previous is not null)
            {
                await _audit.WriteAsync(Entry(CoursesAuditActions.VersionRetired, previous, request, retiredBefore), cancellationToken);
            }

            return committed;
        }

        // Active -> Retired with the last effective day. Offerings and grades already on the version keep it.
        public async Task<Result<CourseVersionSummary>> RetireAsync(Guid versionId, RetireCourseVersionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(versionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<CourseVersionSummary>.Fail(loaded.Errors);
            }

            if (command.EffectiveTo is not { } effectiveTo)
            {
                return CoursesErrors.EffectiveToInvalid;
            }

            var before = Describe(context.Version);

            var retired = context.Version.Retire(effectiveTo, _clock.UtcNow);
            if (retired.IsFailure)
            {
                return Result<CourseVersionSummary>.Fail(retired.Errors);
            }

            return await CommitAsync(context.Version, Entry(CoursesAuditActions.VersionRetired, context.Version, request, before), cancellationToken);
        }
    }
}
