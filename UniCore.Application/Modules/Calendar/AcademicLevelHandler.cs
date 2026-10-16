using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Modules.Calendar.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Calendar
{
    // Academic levels of a program. The scope is the program's department (and the faculty above it).
    public sealed class AcademicLevelHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IRepository<AcademicLevel> levels, IRepository<AcademicProgram> programs,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<AcademicLevel> _levels = levels;
        private readonly IRepository<AcademicProgram> _programs = programs;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(AcademicLevel l) => $"Program={l.AcademicProgramId};Name={l.Name};Order={l.Order};RequiredCredits={l.RequiredCredits};IsActive={l.IsActive}";

        private async Task<Result<AcademicLevelSummary>> CommitAsync(AcademicLevel level, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "AcademicLevel", level.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(level));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
            return committed.IsFailure ? Result<AcademicLevelSummary>.Fail(committed.Errors) : CalendarMapper.ToSummary(level);
        }

        // Permission first, then the program (to know its department), then the scope. A caller without the
        // permission cannot learn whether a program or level id exists.
        private async Task<Result<AcademicProgram>> AuthorizeForProgramAsync(Guid programId, CancellationToken cancellationToken)
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

            var scope = await _authorizer.AuthorizeAsync(Permissions.University.Manage, ResourceContext.InScope(ScopeType.Department, program.DepartmentId), cancellationToken);
            return scope.IsFailure ? Result<AcademicProgram>.Fail(scope.Errors) : program;
        }

        private async Task<Result<AcademicLevel>> LoadForManageAsync(Guid levelId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.University.Manage, null, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<AcademicLevel>.Fail(permission.Errors);
            }

            var level = await _levels.GetByIdAsync(levelId, cancellationToken);
            if (level is null)
            {
                return CalendarErrors.LevelNotFound;
            }

            var program = await AuthorizeForProgramAsync(level.AcademicProgramId, cancellationToken);
            return program.IsFailure ? Result<AcademicLevel>.Fail(program.Errors) : level;
        }

        public async Task<Result<PagedResult<AcademicLevelSummary>>> ListAsync(PaginationQuery query, string? search, string? sort, bool? isActive, Guid? academicProgramId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<AcademicLevelSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, AcademicLevelsListSpecification.SortFields, "order");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<AcademicLevelSummary>>.Fail(parsed.Errors);
            }

            var specification = new AcademicLevelsListSpecification(query, ListQueryRules.NormalizeSearch(search), isActive, academicProgramId, sortOption);
            var items = await _levels.ListAsync(specification, cancellationToken);
            var total = await _levels.CountAsync(specification, cancellationToken);

            return Result<PagedResult<AcademicLevelSummary>>.Ok(PagedResult<AcademicLevelSummary>.Create(items.Select(x => CalendarMapper.ToSummary(x)).ToList(), query, total));
        }

        public async Task<Result<AcademicLevelSummary>> GetAsync(Guid levelId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<AcademicLevelSummary>.Fail(auth.Errors);
            }

            var level = await _levels.GetByIdAsync(levelId, cancellationToken);
            return level is null ? CalendarErrors.LevelNotFound : CalendarMapper.ToSummary(level);
        }

        public async Task<Result<AcademicLevelSummary>> CreateAsync(CreateAcademicLevelCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            if (command.AcademicProgramId == Guid.Empty)
            {
                // Still check the permission first, so the answer does not depend on who asks.
                var permission = await _authorizer.AuthorizeAsync(Permissions.University.Manage, null, cancellationToken);
                return permission.IsFailure ? Result<AcademicLevelSummary>.Fail(permission.Errors) : CalendarErrors.InvalidParent;
            }

            var authorized = await AuthorizeForProgramAsync(command.AcademicProgramId, cancellationToken);
            if (!authorized.TryGetValue(out var program))
            {
                return Result<AcademicLevelSummary>.Fail(authorized.Errors);
            }

            if (!program.IsActive)
            {
                return UniversityErrors.ProgramInactive;
            }

            var created = AcademicLevel.Create(program.Id, command.Name, command.Order, command.RequiredCredits, _clock.UtcNow);
            if (!created.TryGetValue(out var level))
            {
                return Result<AcademicLevelSummary>.Fail(created.Errors);
            }

            _levels.Add(level);
            return await CommitAsync(level, CalendarAuditActions.LevelCreated, null, request, cancellationToken);
        }

        public async Task<Result<AcademicLevelSummary>> UpdateAsync(Guid levelId, UpdateAcademicLevelCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(levelId, cancellationToken);
            if (!loaded.TryGetValue(out var level))
            {
                return Result<AcademicLevelSummary>.Fail(loaded.Errors);
            }

            var before = Describe(level);
            var updated = level.Update(command.Name, command.Order, command.RequiredCredits, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<AcademicLevelSummary>.Fail(updated.Errors);
            }

            return await CommitAsync(level, CalendarAuditActions.LevelUpdated, before, request, cancellationToken);
        }

        public async Task<Result<AcademicLevelSummary>> ActivateAsync(Guid levelId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(levelId, cancellationToken);
            if (!loaded.TryGetValue(out var level))
            {
                return Result<AcademicLevelSummary>.Fail(loaded.Errors);
            }

            var before = Describe(level);
            var result = level.Activate(_clock.UtcNow);
            if (result.IsFailure)
            {
                return Result<AcademicLevelSummary>.Fail(result.Errors);
            }

            return await CommitAsync(level, CalendarAuditActions.LevelActivated, before, request, cancellationToken);
        }

        public async Task<Result<AcademicLevelSummary>> DeactivateAsync(Guid levelId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(levelId, cancellationToken);
            if (!loaded.TryGetValue(out var level))
            {
                return Result<AcademicLevelSummary>.Fail(loaded.Errors);
            }

            var before = Describe(level);
            var result = level.Deactivate(_clock.UtcNow);
            if (result.IsFailure)
            {
                return Result<AcademicLevelSummary>.Fail(result.Errors);
            }

            return await CommitAsync(level, CalendarAuditActions.LevelDeactivated, before, request, cancellationToken);
        }
    }
}
