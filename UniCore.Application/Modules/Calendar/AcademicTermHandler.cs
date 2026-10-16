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
    // Academic terms (Fall, Spring, Summer). Changes need University.Manage at the top level.
    public sealed class AcademicTermHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user, IRepository<AcademicTerm> terms,
        IUnitOfWork unitOfWork, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<AcademicTerm> _terms = terms;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(AcademicTerm t) => $"Code={t.Code};Name={t.Name};SortOrder={t.SortOrder};IsActive={t.IsActive}";

        private Task<Result> AuthorizeManageAsync(CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.University.Manage, GlobalResource, cancellationToken);

        private async Task<Result<AcademicTermSummary>> CommitAsync(AcademicTerm term, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "AcademicTerm", term.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(term));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
            return committed.IsFailure ? Result<AcademicTermSummary>.Fail(committed.Errors) : CalendarMapper.ToSummary(term);
        }

        private async Task<Result<AcademicTerm>> LoadForManageAsync(Guid termId, CancellationToken cancellationToken)
        {
            var auth = await AuthorizeManageAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<AcademicTerm>.Fail(auth.Errors);
            }

            var term = await _terms.GetByIdAsync(termId, cancellationToken);
            return term is null ? CalendarErrors.AcademicTermNotFound : term;
        }

        public async Task<Result<PagedResult<AcademicTermSummary>>> ListAsync(PaginationQuery query, string? search, string? sort, bool? isActive, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<AcademicTermSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, AcademicTermsListSpecification.SortFields, "sortOrder");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<AcademicTermSummary>>.Fail(parsed.Errors);
            }

            var specification = new AcademicTermsListSpecification(query, ListQueryRules.NormalizeSearch(search), isActive, sortOption);
            var items = await _terms.ListAsync(specification, cancellationToken);
            var total = await _terms.CountAsync(specification, cancellationToken);

            return Result<PagedResult<AcademicTermSummary>>.Ok(PagedResult<AcademicTermSummary>.Create(items.Select(x => CalendarMapper.ToSummary(x)).ToList(), query, total));
        }

        public async Task<Result<AcademicTermSummary>> GetAsync(Guid termId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.University.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<AcademicTermSummary>.Fail(auth.Errors);
            }

            var term = await _terms.GetByIdAsync(termId, cancellationToken);
            return term is null ? CalendarErrors.AcademicTermNotFound : CalendarMapper.ToSummary(term);
        }

        public async Task<Result<AcademicTermSummary>> CreateAsync(CreateAcademicTermCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await AuthorizeManageAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<AcademicTermSummary>.Fail(auth.Errors);
            }

            var created = AcademicTerm.Create(command.Code, command.Name, command.SortOrder);
            if (!created.TryGetValue(out var term))
            {
                return Result<AcademicTermSummary>.Fail(created.Errors);
            }

            _terms.Add(term);
            return await CommitAsync(term, CalendarAuditActions.TermCreated, null, request, cancellationToken);
        }

        public async Task<Result<AcademicTermSummary>> UpdateAsync(Guid termId, UpdateAcademicTermCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(termId, cancellationToken);
            if (!loaded.TryGetValue(out var term))
            {
                return Result<AcademicTermSummary>.Fail(loaded.Errors);
            }

            var before = Describe(term);
            var updated = term.Update(command.Name, command.SortOrder);
            if (updated.IsFailure)
            {
                return Result<AcademicTermSummary>.Fail(updated.Errors);
            }

            return await CommitAsync(term, CalendarAuditActions.TermUpdated, before, request, cancellationToken);
        }

        public async Task<Result<AcademicTermSummary>> ActivateAsync(Guid termId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(termId, cancellationToken);
            if (!loaded.TryGetValue(out var term))
            {
                return Result<AcademicTermSummary>.Fail(loaded.Errors);
            }

            var before = Describe(term);
            var result = term.Activate();
            if (result.IsFailure)
            {
                return Result<AcademicTermSummary>.Fail(result.Errors);
            }

            return await CommitAsync(term, CalendarAuditActions.TermActivated, before, request, cancellationToken);
        }

        public async Task<Result<AcademicTermSummary>> DeactivateAsync(Guid termId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(termId, cancellationToken);
            if (!loaded.TryGetValue(out var term))
            {
                return Result<AcademicTermSummary>.Fail(loaded.Errors);
            }

            var before = Describe(term);
            var result = term.Deactivate();
            if (result.IsFailure)
            {
                return Result<AcademicTermSummary>.Fail(result.Errors);
            }

            return await CommitAsync(term, CalendarAuditActions.TermDeactivated, before, request, cancellationToken);
        }
    }
}
