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
    // Person profiles. Reading needs People.Read; changes need People.Manage at the top level.
    // The national id is returned only to callers who hold People.Manage and is never written to the audit log.
    public sealed class PersonProfileHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user, IRepository<PersonProfile> persons,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IRepository<PersonProfile> _persons = persons;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        // No national id here on purpose.
        private static string Describe(PersonProfile p) => $"Name={p.FullName};DateOfBirth={p.DateOfBirth:yyyy-MM-dd};Gender={p.Gender};Email={p.Email};Phone={p.Phone};UserId={p.UserId}";

        private Task<Result> AuthorizeManageAsync(CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.People.Manage, GlobalResource, cancellationToken);

        private async Task<bool> CanSeeNationalIdAsync(CancellationToken cancellationToken)
            => (await _authorizer.AuthorizeAsync(Permissions.People.Manage, null, cancellationToken)).IsSuccess;

        private async Task<Result<PersonSummary>> CommitAsync(PersonProfile person, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "PersonProfile", person.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(person));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
            return committed.IsFailure ? Result<PersonSummary>.Fail(committed.Errors) : PeopleMapper.ToSummary(person, true);
        }

        private async Task<Result<PersonProfile>> LoadForManageAsync(Guid personId, CancellationToken cancellationToken)
        {
            var auth = await AuthorizeManageAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PersonProfile>.Fail(auth.Errors);
            }

            var person = await _persons.GetByIdAsync(personId, cancellationToken);
            return person is null ? PeopleErrors.PersonNotFound : person;
        }

        public async Task<Result<PagedResult<PersonSummary>>> ListAsync(PaginationQuery query, string? search, string? sort, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.People.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<PersonSummary>>.Fail(auth.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, PersonsListSpecification.SortFields, "lastName");
            if (!parsed.TryGetValue(out var sortOption))
            {
                return Result<PagedResult<PersonSummary>>.Fail(parsed.Errors);
            }

            var specification = new PersonsListSpecification(query, ListQueryRules.NormalizeSearch(search), sortOption);
            var items = await _persons.ListAsync(specification, cancellationToken);
            var total = await _persons.CountAsync(specification, cancellationToken);
            var includeNationalId = await CanSeeNationalIdAsync(cancellationToken);

            return Result<PagedResult<PersonSummary>>.Ok(PagedResult<PersonSummary>.Create(items.Select(p => PeopleMapper.ToSummary(p, includeNationalId)).ToList(), query, total));
        }

        public async Task<Result<PersonSummary>> GetAsync(Guid personId, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.People.Read, null, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PersonSummary>.Fail(auth.Errors);
            }

            var person = await _persons.GetByIdAsync(personId, cancellationToken);
            if (person is null)
            {
                return PeopleErrors.PersonNotFound;
            }

            return PeopleMapper.ToSummary(person, await CanSeeNationalIdAsync(cancellationToken));
        }

        public async Task<Result<PersonSummary>> CreateAsync(CreatePersonCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await AuthorizeManageAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PersonSummary>.Fail(auth.Errors);
            }

            var created = PersonProfile.Create(command.FirstName, command.LastName, command.DateOfBirth, command.Gender, command.NationalId, command.Email, command.Phone, _clock.UtcToday, _clock.UtcNow);
            if (!created.TryGetValue(out var person))
            {
                return Result<PersonSummary>.Fail(created.Errors);
            }

            if (person.NationalId is { } nationalId
                && await _persons.AnyAsync(new PersonByNationalIdSpecification(nationalId, null), cancellationToken))
            {
                return PeopleErrors.NationalIdExists;
            }

            _persons.Add(person);
            return await CommitAsync(person, PeopleAuditActions.PersonCreated, null, request, cancellationToken);
        }

        public async Task<Result<PersonSummary>> UpdateAsync(Guid personId, UpdatePersonCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(personId, cancellationToken);
            if (!loaded.TryGetValue(out var person))
            {
                return Result<PersonSummary>.Fail(loaded.Errors);
            }

            // A national id that another person already has is rejected before anything changes.
            var newNationalId = command.NationalId?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(newNationalId)
                && await _persons.AnyAsync(new PersonByNationalIdSpecification(newNationalId, person.Id), cancellationToken))
            {
                return PeopleErrors.NationalIdExists;
            }

            var before = Describe(person);
            var updated = person.Update(command.FirstName, command.LastName, command.DateOfBirth, command.Gender, command.NationalId, command.Email, command.Phone, _clock.UtcToday, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<PersonSummary>.Fail(updated.Errors);
            }

            return await CommitAsync(person, PeopleAuditActions.PersonUpdated, before, request, cancellationToken);
        }

        // Links the person to a login account (Identity module). Each person and each user can be linked once.
        public async Task<Result<PersonSummary>> LinkUserAsync(Guid personId, Guid userId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForManageAsync(personId, cancellationToken);
            if (!loaded.TryGetValue(out var person))
            {
                return Result<PersonSummary>.Fail(loaded.Errors);
            }

            if (userId != Guid.Empty
                && await _persons.AnyAsync(new PersonByUserSpecification(userId), cancellationToken))
            {
                return PeopleErrors.UserAlreadyLinked;
            }

            var before = Describe(person);
            var linked = person.LinkUser(userId, _clock.UtcNow);
            if (linked.IsFailure)
            {
                return Result<PersonSummary>.Fail(linked.Errors);
            }

            return await CommitAsync(person, PeopleAuditActions.PersonUserLinked, before, request, cancellationToken);
        }
    }
}
