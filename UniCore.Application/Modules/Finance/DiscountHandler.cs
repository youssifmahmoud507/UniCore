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
    public sealed class DiscountHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<Discount> discounts, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private const int MaxSearchLength = 30;
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Discount> _discounts = discounts;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task<Result> RequireAsync(string permission, CancellationToken cancellationToken) => _authorizer.AuthorizeAsync(permission, GlobalResource, cancellationToken);

        private Task AuditAsync(string action, Guid entityId, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken) => _audit.WriteAsync(
            new AuditEntry(action, _user.UserId, "Discount", entityId.ToString(), request.IpAddress, request.UserAgent, oldValues, newValues),
            cancellationToken);

        private async Task<Result<Discount>> LoadAsync(Guid id, CancellationToken cancellationToken)
        {
            var discount = await _discounts.FirstOrDefaultAsync(new DiscountByIdSpecification(id, tracking: true), cancellationToken);

            return discount is null ? FinanceErrors.DiscountNotFound : discount;
        }

        public async Task<Result<DiscountDto>> CreateAsync(CreateDiscountCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Fees.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<DiscountDto>.Fail(auth.Errors);
            }

            if (command.Type is not { } type)
            {
                return FinanceErrors.ReductionDataInvalid;
            }

            if (command.Value is not { } value)
            {
                return FinanceErrors.AmountRequired;
            }

            var code = FeeDefinition.NormalizeCode(command.Code);
            if (code is null)
            {
                return FinanceErrors.ReductionCodeInvalid;
            }

            if (await _discounts.AnyAsync(new DiscountByCodeSpecification(code), cancellationToken))
            {
                return FinanceErrors.DiscountCodeAlreadyExists;
            }

            var created = Discount.Create(code, command.Name, command.Description, type, value, command.ValidFrom, command.ValidTo, _clock.UtcNow);
            if (!created.TryGetValue(out var discount))
            {
                return Result<DiscountDto>.Fail(created.Errors);
            }

            _discounts.Add(discount);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error?.Code == CommonErrors.DuplicateResource.Code
                    ? FinanceErrors.DiscountCodeAlreadyExists
                    : Result<DiscountDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.DiscountCreated, discount.Id, request, null, $"{discount.Code} {discount.Type} {discount.Value}", cancellationToken);

            return ReductionsMapper.ToDto(discount);
        }

        public async Task<Result<DiscountDto>> UpdateAsync(Guid id, UpdateDiscountCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Fees.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<DiscountDto>.Fail(auth.Errors);
            }

            if (command.Type is not { } type)
            {
                return FinanceErrors.ReductionDataInvalid;
            }

            if (command.Value is not { } value)
            {
                return FinanceErrors.AmountRequired;
            }

            var loaded = await LoadAsync(id, cancellationToken);
            if (!loaded.TryGetValue(out var discount))
            {
                return Result<DiscountDto>.Fail(loaded.Errors);
            }

            var oldValues = $"{discount.Name}; {discount.Type} {discount.Value}";

            var updated = discount.Update(command.Name, command.Description, type, value, command.ValidFrom, command.ValidTo, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<DiscountDto>.Fail(updated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<DiscountDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.DiscountUpdated, discount.Id, request, oldValues, $"{discount.Name}; {discount.Type} {discount.Value}", cancellationToken);

            return ReductionsMapper.ToDto(discount);
        }

        public async Task<Result<DiscountDto>> SetActiveAsync(Guid id, bool active, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Fees.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<DiscountDto>.Fail(auth.Errors);
            }

            var loaded = await LoadAsync(id, cancellationToken);
            if (!loaded.TryGetValue(out var discount))
            {
                return Result<DiscountDto>.Fail(loaded.Errors);
            }

            if (discount.IsActive == active)
            {
                return ReductionsMapper.ToDto(discount);
            }

            var changed = discount.SetActive(active, _clock.UtcNow);
            if (changed.IsFailure)
            {
                return Result<DiscountDto>.Fail(changed.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<DiscountDto>.Fail(saved.Errors);
            }

            await AuditAsync(active ? AuditActions.DiscountActivated : AuditActions.DiscountDeactivated, discount.Id, request, null, discount.Code, cancellationToken);

            return ReductionsMapper.ToDto(discount);
        }

        public async Task<Result<PagedResult<DiscountDto>>> ListAsync(PaginationQuery query, bool? isActive, string? search, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<DiscountDto>>.Fail(auth.Errors);
            }

            var term = search?.Trim();
            if (term is { Length: > MaxSearchLength })
            {
                term = term[..MaxSearchLength];
            }

            var specification = new DiscountsListSpecification(query, isActive, string.IsNullOrEmpty(term) ? null : term);

            var items = await _discounts.ListAsync(specification, cancellationToken);
            var total = await _discounts.CountAsync(specification, cancellationToken);

            return PagedResult<DiscountDto>.Create(items.Select(d => ReductionsMapper.ToDto(d)).ToList(), query, total);
        }

        public async Task<Result<DiscountDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<DiscountDto>.Fail(auth.Errors);
            }

            var discount = await _discounts.FirstOrDefaultAsync(new DiscountByIdSpecification(id), cancellationToken);

            return discount is null ? FinanceErrors.DiscountNotFound : ReductionsMapper.ToDto(discount);
        }
    }






}
