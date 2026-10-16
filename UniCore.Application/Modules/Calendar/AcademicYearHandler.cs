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

namespace UniCore.Application.Modules.Calendar
{
    // Academic years. Reading needs University.Read; every change needs University.Manage at the top level (the calendar is institution-wide).
    public sealed class AcademicYearHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IRepository<AcademicYear> years, IRepository<AcademicSemester> semesters,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<AcademicYear> _years = years;
        private readonly IRepository<AcademicSemester> _semesters = semesters;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(AcademicYear y) => $"Name={y.Name};Start={y.StartDate:yyyy-MM-dd};End={y.EndDate:yyyy-MM-dd};Status={y.Status};IsCurrent={y.IsCurrent}";

        private Task<Result> AuthorizeManageAsync(CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.University.Manage, GlobalResource, cancellationToken);

        private async Task<Result<AcademicYear>> LoadForManageAsync(Guid yearId, CancellationToken cancellationToken)
        {
            var auth = await AuthorizeManageAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<AcademicYear>.Fail(auth.Errors);
            }

            var year = await _years.GetByIdAsync(yearId, cancellationToken);
            return year is null ? CalendarErrors.AcademicYearNotFound : year;
        }

        private async Task<Result<AcademicYearSummary>> CommitAsync(AcademicYear year, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "AcademicYear", year.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(year));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
            return committed.IsFailure ? Result<AcademicYearSummary>.Fail(committed.Errors) : CalendarMapper.ToSummary(year);
        }

        public async Task<Result<PagedResult<AcademicYearSummary>>> ListAsync(PaginationQuery query, string? search, string? sort, AcademicYearStatus? status, bool? isCurrent, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<AcademicYearSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, AcademicYearsListSpecification.SortFields, "-startDate");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<AcademicYearSummary>>.Fail(parsed.Errors);
            }

            var specification = new AcademicYearsListSpecification(query, ListQueryRules.NormalizeSearch(search), status, isCurrent, sortOption);
            var items = await _years.ListAsync(specification, cancellationToken);
            var total = await _years.CountAsync(specification, cancellationToken);

            return Result<PagedResult<AcademicYearSummary>>.Ok(PagedResult<AcademicYearSummary>.Create(items.Select(x => CalendarMapper.ToSummary(x)).ToList(), query, total));
        }

        public async Task<Result<AcademicYearSummary>> GetAsync(Guid yearId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<AcademicYearSummary>.Fail(auth.Errors);
            }

            var year = await _years.GetByIdAsync(yearId, cancellationToken);
            return year is null ? CalendarErrors.AcademicYearNotFound : CalendarMapper.ToSummary(year);
        }

        public async Task<Result<AcademicYearSummary>> CreateAsync(CreateAcademicYearCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await AuthorizeManageAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<AcademicYearSummary>.Fail(auth.Errors);
            }

            var created = AcademicYear.Create(command.Name, command.StartDate, command.EndDate, _clock.UtcNow);
            if (!created.TryGetValue(out var year))
            {
                return Result<AcademicYearSummary>.Fail(created.Errors);
            }

            if (await _years.AnyAsync(new OverlappingYearsSpecification(year.StartDate, year.EndDate, null), cancellationToken))
            {
                return CalendarErrors.AcademicYearOverlap;
            }

            _years.Add(year);
            return await CommitAsync(year, CalendarAuditActions.YearCreated, null, request, cancellationToken);
        }

        public async Task<Result<AcademicYearSummary>> UpdateAsync(Guid yearId, UpdateAcademicYearCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(yearId, cancellationToken);
            if (!loaded.TryGetValue(out var year))
            {
                return Result<AcademicYearSummary>.Fail(loaded.Errors);
            }

            // These two lookups only make sense for a valid range; an invalid one is rejected by the entity below.
            if (command.StartDate < command.EndDate && year.Status != AcademicYearStatus.Closed)
            {
                if (await _years.AnyAsync(new OverlappingYearsSpecification(command.StartDate, command.EndDate, year.Id), cancellationToken))
                {
                    return CalendarErrors.AcademicYearOverlap;
                }

                if (await _semesters.AnyAsync(new SemestersOutsideRangeSpecification(year.Id, command.StartDate, command.EndDate), cancellationToken))
                {
                    return CalendarErrors.SemesterOutsideYear;
                }
            }

            var before = Describe(year);
            var updated = year.UpdateDetails(command.Name, command.StartDate, command.EndDate, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<AcademicYearSummary>.Fail(updated.Errors);
            }

            return await CommitAsync(year, CalendarAuditActions.YearUpdated, before, request, cancellationToken);
        }

        public async Task<Result<AcademicYearSummary>> ActivateAsync(Guid yearId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(yearId, cancellationToken);
            if (!loaded.TryGetValue(out var year))
            {
                return Result<AcademicYearSummary>.Fail(loaded.Errors);
            }

            var before = Describe(year);
            var activated = year.Activate(_clock.UtcNow);
            if (activated.IsFailure)
            {
                return Result<AcademicYearSummary>.Fail(activated.Errors);
            }

            return await CommitAsync(year, CalendarAuditActions.YearActivated, before, request, cancellationToken);
        }

        public async Task<Result<AcademicYearSummary>> MakeCurrentAsync(Guid yearId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(yearId, cancellationToken);
            if (!loaded.TryGetValue(out var year))
            {
                return Result<AcademicYearSummary>.Fail(loaded.Errors);
            }

            var another = await _years.AnyAsync(new CurrentYearSpecification(year.Id), cancellationToken);
            var before = Describe(year);
            var made = year.MakeCurrent(another, _clock.UtcNow);
            if (made.IsFailure)
            {
                return Result<AcademicYearSummary>.Fail(made.Errors);
            }

            return await CommitAsync(year, CalendarAuditActions.YearMadeCurrent, before, request, cancellationToken);
        }

        public async Task<Result<AcademicYearSummary>> CloseAsync(Guid yearId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(yearId, cancellationToken);
            if (!loaded.TryGetValue(out var year))
            {
                return Result<AcademicYearSummary>.Fail(loaded.Errors);
            }

            var hasOpen = await _semesters.AnyAsync(new OpenSemestersOfYearSpecification(year.Id), cancellationToken);
            var before = Describe(year);
            var closed = year.Close(hasOpen, _clock.UtcNow);
            if (closed.IsFailure)
            {
                return Result<AcademicYearSummary>.Fail(closed.Errors);
            }

            return await CommitAsync(year, CalendarAuditActions.YearClosed, before, request, cancellationToken);
        }
    }
}
