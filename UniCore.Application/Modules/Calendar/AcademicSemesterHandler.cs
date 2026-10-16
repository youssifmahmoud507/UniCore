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
    // Semesters and their date windows. Changes need University.Manage at the top level.
    public sealed class AcademicSemesterHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user,
        IRepository<AcademicSemester> semesters, IRepository<AcademicYear> years, IRepository<AcademicTerm> terms,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<AcademicSemester> _semesters = semesters;
        private readonly IRepository<AcademicYear> _years = years;
        private readonly IRepository<AcademicTerm> _terms = terms;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(AcademicSemester s) => $"Year={s.AcademicYearId};Term={s.AcademicTermId};Start={s.StartDate:yyyy-MM-dd};End={s.EndDate:yyyy-MM-dd};Status={s.Status};"
            + $"Registration={s.RegistrationStart:yyyy-MM-dd}..{s.RegistrationEnd:yyyy-MM-dd};AddDrop={s.AddDropStart:yyyy-MM-dd}..{s.AddDropEnd:yyyy-MM-dd};Withdrawal={s.WithdrawalDeadline:yyyy-MM-dd};"
            + $"Midterm={s.MidtermStart:yyyy-MM-dd}..{s.MidtermEnd:yyyy-MM-dd};Final={s.FinalExamStart:yyyy-MM-dd}..{s.FinalExamEnd:yyyy-MM-dd};"
            + $"GradeDeadline={s.GradeSubmissionDeadline:yyyy-MM-dd};GradePublication={s.GradePublicationDate:yyyy-MM-dd}";

        private Task<Result> AuthorizeManageAsync(CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.University.Manage, GlobalResource, cancellationToken);

        private async Task<Result<SemesterSummary>> CommitAsync(AcademicSemester semester, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "AcademicSemester", semester.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(semester));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
            return committed.IsFailure ? Result<SemesterSummary>.Fail(committed.Errors) : CalendarMapper.ToSummary(semester);
        }

        private async Task<Result<AcademicSemester>> LoadForManageAsync(Guid semesterId, CancellationToken cancellationToken)
        {
            var auth = await AuthorizeManageAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<AcademicSemester>.Fail(auth.Errors);
            }

            var semester = await _semesters.GetByIdAsync(semesterId, cancellationToken);
            return semester is null ? CalendarErrors.SemesterNotFound : semester;
        }

        public async Task<Result<PagedResult<SemesterSummary>>> ListAsync(PaginationQuery query, string? sort, Guid? academicYearId, Guid? academicTermId, SemesterStatus? status, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<SemesterSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, SemestersListSpecification.SortFields, "-startDate");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<SemesterSummary>>.Fail(parsed.Errors);
            }

            var specification = new SemestersListSpecification(query, academicYearId, academicTermId, status, sortOption);
            var items = await _semesters.ListAsync(specification, cancellationToken);
            var total = await _semesters.CountAsync(specification, cancellationToken);

            return Result<PagedResult<SemesterSummary>>.Ok(PagedResult<SemesterSummary>.Create(items.Select(x => CalendarMapper.ToSummary(x)).ToList(), query, total));
        }

        public async Task<Result<SemesterSummary>> GetAsync(Guid semesterId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<SemesterSummary>.Fail(auth.Errors);
            }

            var semester = await _semesters.GetByIdAsync(semesterId, cancellationToken);
            return semester is null ? CalendarErrors.SemesterNotFound : CalendarMapper.ToSummary(semester);
        }

        public async Task<Result<SemesterSummary>> CreateAsync(CreateSemesterCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await AuthorizeManageAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<SemesterSummary>.Fail(auth.Errors);
            }

            if (command.AcademicYearId == Guid.Empty || command.AcademicTermId == Guid.Empty)
            {
                return CalendarErrors.InvalidParent;
            }

            var year = await _years.GetByIdAsync(command.AcademicYearId, cancellationToken);
            if (year is null)
            {
                return CalendarErrors.AcademicYearNotFound;
            }

            if (year.Status == AcademicYearStatus.Closed)
            {
                return CalendarErrors.AcademicYearClosed;
            }

            var term = await _terms.GetByIdAsync(command.AcademicTermId, cancellationToken);
            if (term is null)
            {
                return CalendarErrors.AcademicTermNotFound;
            }

            if (!term.IsActive)
            {
                return CalendarErrors.TermInactive;
            }

            if (await _semesters.AnyAsync(new SemesterOfYearAndTermSpecification(year.Id, term.Id), cancellationToken))
            {
                return CalendarErrors.SemesterDuplicateTerm;
            }

            var created = AcademicSemester.Create(year.Id, term.Id, command.StartDate, command.EndDate, command.Windows, year.StartDate, year.EndDate, _clock.UtcNow);
            if (!created.TryGetValue(out var semester))
            {
                return Result<SemesterSummary>.Fail(created.Errors);
            }

            if (await _semesters.AnyAsync(new OverlappingSemestersSpecification(year.Id, semester.StartDate, semester.EndDate, null), cancellationToken))
            {
                return CalendarErrors.SemesterOverlap;
            }

            _semesters.Add(semester);
            return await CommitAsync(semester, CalendarAuditActions.SemesterCreated, null, request, cancellationToken);
        }

        public async Task<Result<SemesterSummary>> UpdateAsync(Guid semesterId, UpdateSemesterCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(semesterId, cancellationToken);
            if (!loaded.TryGetValue(out var semester))
            {
                return Result<SemesterSummary>.Fail(loaded.Errors);
            }

            var year = await _years.GetByIdAsync(semester.AcademicYearId, cancellationToken);
            if (year is null)
            {
                return CalendarErrors.AcademicYearNotFound;
            }

            if (year.Status == AcademicYearStatus.Closed)
            {
                return CalendarErrors.AcademicYearClosed;
            }

            if (command.StartDate < command.EndDate
                && await _semesters.AnyAsync(new OverlappingSemestersSpecification(year.Id, command.StartDate, command.EndDate, semester.Id), cancellationToken))
            {
                return CalendarErrors.SemesterOverlap;
            }

            var before = Describe(semester);
            var rescheduled = semester.Reschedule(command.StartDate, command.EndDate, command.Windows, year.StartDate, year.EndDate, _clock.UtcNow);
            if (rescheduled.IsFailure)
            {
                return Result<SemesterSummary>.Fail(rescheduled.Errors);
            }

            return await CommitAsync(semester, CalendarAuditActions.SemesterRescheduled, before, request, cancellationToken);
        }

        // Moves the semester to a later status. A missing target is a validation error.
        public async Task<Result<SemesterSummary>> AdvanceStatusAsync(Guid semesterId, SemesterStatus? target, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(semesterId, cancellationToken);
            if (!loaded.TryGetValue(out var semester))
            {
                return Result<SemesterSummary>.Fail(loaded.Errors);
            }

            if (target is not { } newStatus)
            {
                return CalendarErrors.InvalidSemesterStatus;
            }

            var before = Describe(semester);
            var advanced = semester.AdvanceTo(newStatus, _clock.UtcNow);
            if (advanced.IsFailure)
            {
                return Result<SemesterSummary>.Fail(advanced.Errors);
            }

            return await CommitAsync(semester, CalendarAuditActions.SemesterStatusChanged, before, request, cancellationToken);
        }
    }
}
