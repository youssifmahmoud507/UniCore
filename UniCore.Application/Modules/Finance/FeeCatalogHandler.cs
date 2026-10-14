using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Finance
{
    public sealed class FeeCatalogHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<FeeDefinition> definitions, IFinanceReferenceVerifier verifier, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private const int MaxSearchLength = 30;

        // Global scope for now, like the student handler: scoped roles are denied rather than allowed too much.
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<FeeDefinition> _definitions = definitions;
        private readonly IFinanceReferenceVerifier _verifier = verifier;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task<Result> RequireAsync(string permission, CancellationToken cancellationToken) => _authorizer.AuthorizeAsync(permission, GlobalResource, cancellationToken);

        private Task AuditAsync(string action, Guid entityId, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken) => _audit.WriteAsync(new AuditEntry(action, _user.UserId, "FeeDefinition", entityId.ToString(), request.IpAddress, request.UserAgent, oldValues, newValues), cancellationToken);

        private async Task<Result<FeeDefinition>> LoadAsync(Guid id, CancellationToken cancellationToken)
        {
            var definition = await _definitions.FirstOrDefaultAsync(new FeeDefinitionByIdSpecification(id, tracking: true), cancellationToken);

            return definition is null ? FinanceErrors.FeeDefinitionNotFound : definition;
        }

        // ---------- changes ----------

        public async Task<Result<FeeDefinitionDto>> CreateAsync(CreateFeeDefinitionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Fees.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FeeDefinitionDto>.Fail(auth.Errors);
            }

            if (command.DefaultAmount is not { } rawAmount)
            {
                return FinanceErrors.AmountRequired;
            }

            var parsed = Money.Of(rawAmount);
            if (!parsed.TryGetValue(out var amount))
            {
                return Result<FeeDefinitionDto>.Fail(parsed.Errors);
            }

            var code = FeeDefinition.NormalizeCode(command.Code);
            if (code is null)
            {
                return FinanceErrors.FeeCodeInvalid;
            }

            var university = await _verifier.VerifyUniversityAsync(command.UniversityId, cancellationToken);
            if (university.IsFailure)
            {
                return Result<FeeDefinitionDto>.Fail(university.Errors);
            }

            if (await _definitions.AnyAsync(new FeeDefinitionByCodeSpecification(command.UniversityId, code), cancellationToken))
            {
                return FinanceErrors.FeeCodeAlreadyExists;
            }

            var created = FeeDefinition.Create(command.UniversityId, code, command.Name, command.Description, amount, _clock.UtcNow);
            if (!created.TryGetValue(out var definition))
            {
                return Result<FeeDefinitionDto>.Fail(created.Errors);
            }

            _definitions.Add(definition);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FeeDefinitionDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.FeeDefinitionCreated, definition.Id, request, null, $"{definition.Code} {definition.DefaultAmount}", cancellationToken);

            return FinanceMapper.ToDto(definition);
        }

        public async Task<Result<FeeDefinitionDto>> UpdateAsync(Guid id, UpdateFeeDefinitionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Fees.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FeeDefinitionDto>.Fail(auth.Errors);
            }

            if (command.DefaultAmount is not { } rawAmount)
            {
                return FinanceErrors.AmountRequired;
            }

            var parsed = Money.Of(rawAmount);
            if (!parsed.TryGetValue(out var amount))
            {
                return Result<FeeDefinitionDto>.Fail(parsed.Errors);
            }

            var loaded = await LoadAsync(id, cancellationToken);
            if (!loaded.TryGetValue(out var definition))
            {
                return Result<FeeDefinitionDto>.Fail(loaded.Errors);
            }

            var oldValues = $"{definition.Name}; {definition.DefaultAmount}";

            var updated = definition.Update(command.Name, command.Description, amount, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<FeeDefinitionDto>.Fail(updated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FeeDefinitionDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.FeeDefinitionUpdated, definition.Id, request, oldValues, $"{definition.Name}; {definition.DefaultAmount}", cancellationToken);

            return FinanceMapper.ToDto(definition);
        }

        public async Task<Result<FeeDefinitionDto>> SetActiveAsync(Guid id, bool active, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Fees.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FeeDefinitionDto>.Fail(auth.Errors);
            }

            var loaded = await LoadAsync(id, cancellationToken);
            if (!loaded.TryGetValue(out var definition))
            {
                return Result<FeeDefinitionDto>.Fail(loaded.Errors);
            }

            if (definition.IsActive == active)
            {
                return FinanceMapper.ToDto(definition);
            }

            var changed = definition.SetActive(active, _clock.UtcNow);
            if (changed.IsFailure)
            {
                return Result<FeeDefinitionDto>.Fail(changed.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FeeDefinitionDto>.Fail(saved.Errors);
            }

            await AuditAsync(active ? AuditActions.FeeDefinitionActivated : AuditActions.FeeDefinitionDeactivated, definition.Id, request, null, definition.Code, cancellationToken);

            return FinanceMapper.ToDto(definition);
        }

        // ---------- queries ----------

        public async Task<Result<PagedResult<FeeDefinitionDto>>> ListAsync(PaginationQuery query, bool? isActive, string? search, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<FeeDefinitionDto>>.Fail(auth.Errors);
            }

            var term = search?.Trim();
            if (term is { Length: > MaxSearchLength })
            {
                term = term[..MaxSearchLength];
            }

            var specification = new FeeDefinitionsListSpecification(query, isActive, string.IsNullOrEmpty(term) ? null : term);

            var items = await _definitions.ListAsync(specification, cancellationToken);
            var total = await _definitions.CountAsync(specification, cancellationToken);

            return PagedResult<FeeDefinitionDto>.Create(items.Select(FinanceMapper.ToDto).ToList(), query, total);
        }

        public async Task<Result<FeeDefinitionDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FeeDefinitionDto>.Fail(auth.Errors);
            }

            var definition = await _definitions.FirstOrDefaultAsync(new FeeDefinitionByIdSpecification(id), cancellationToken);

            return definition is null ? FinanceErrors.FeeDefinitionNotFound : FinanceMapper.ToDto(definition);
        }
    }

}
