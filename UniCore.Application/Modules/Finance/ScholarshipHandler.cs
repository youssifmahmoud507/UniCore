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
    public sealed class ScholarshipHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<Scholarship> scholarships, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private const int MaxSearchLength = 30;
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Scholarship> _scholarships = scholarships;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task<Result> RequireAsync(string permission, CancellationToken cancellationToken) => _authorizer.AuthorizeAsync(permission, GlobalResource, cancellationToken);

        private Task AuditAsync(string action, Guid entityId, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken) => _audit.WriteAsync(new AuditEntry(action, _user.UserId, "Scholarship", entityId.ToString(), request.IpAddress, request.UserAgent, oldValues, newValues), cancellationToken);

        private async Task<Result<Scholarship>> LoadAsync(Guid id, CancellationToken cancellationToken)
        {
            var scholarship = await _scholarships.FirstOrDefaultAsync(new ScholarshipByIdSpecification(id, tracking: true), cancellationToken);

            return scholarship is null ? FinanceErrors.ScholarshipNotFound : scholarship;
        }

        public async Task<Result<ScholarshipDto>> CreateAsync(CreateScholarshipCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Scholarships.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<ScholarshipDto>.Fail(auth.Errors);
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

            if (await _scholarships.AnyAsync(new ScholarshipByCodeSpecification(code), cancellationToken))
            {
                return FinanceErrors.ScholarshipCodeAlreadyExists;
            }

            var created = Scholarship.Create(code, command.Name, command.Description, type, value, _clock.UtcNow);
            if (!created.TryGetValue(out var scholarship))
            {
                return Result<ScholarshipDto>.Fail(created.Errors);
            }

            _scholarships.Add(scholarship);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error?.Code == CommonErrors.DuplicateResource.Code
                    ? FinanceErrors.ScholarshipCodeAlreadyExists
                    : Result<ScholarshipDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.ScholarshipCreated, scholarship.Id, request, null, $"{scholarship.Code} {scholarship.Type} {scholarship.Value}", cancellationToken);

            return ReductionsMapper.ToDto(scholarship);
        }

        public async Task<Result<ScholarshipDto>> UpdateAsync(Guid id, UpdateScholarshipCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Scholarships.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<ScholarshipDto>.Fail(auth.Errors);
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
            if (!loaded.TryGetValue(out var scholarship))
            {
                return Result<ScholarshipDto>.Fail(loaded.Errors);
            }

            var oldValues = $"{scholarship.Name}; {scholarship.Type} {scholarship.Value}";

            var updated = scholarship.Update(command.Name, command.Description, type, value, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<ScholarshipDto>.Fail(updated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<ScholarshipDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.ScholarshipUpdated, scholarship.Id, request, oldValues, $"{scholarship.Name}; {scholarship.Type} {scholarship.Value}", cancellationToken);

            return ReductionsMapper.ToDto(scholarship);
        }

        public async Task<Result<ScholarshipDto>> SetActiveAsync(Guid id, bool active, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Scholarships.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<ScholarshipDto>.Fail(auth.Errors);
            }

            var loaded = await LoadAsync(id, cancellationToken);
            if (!loaded.TryGetValue(out var scholarship))
            {
                return Result<ScholarshipDto>.Fail(loaded.Errors);
            }

            if (scholarship.IsActive == active)
            {
                return ReductionsMapper.ToDto(scholarship);
            }

            var changed = scholarship.SetActive(active, _clock.UtcNow);
            if (changed.IsFailure)
            {
                return Result<ScholarshipDto>.Fail(changed.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<ScholarshipDto>.Fail(saved.Errors);
            }

            await AuditAsync(active ? AuditActions.ScholarshipActivated : AuditActions.ScholarshipDeactivated, scholarship.Id, request, null, scholarship.Code, cancellationToken);

            return ReductionsMapper.ToDto(scholarship);
        }

        public async Task<Result<PagedResult<ScholarshipDto>>> ListAsync(PaginationQuery query, bool? isActive, string? search, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<ScholarshipDto>>.Fail(auth.Errors);
            }

            var term = search?.Trim();
            if (term is { Length: > MaxSearchLength })
            {
                term = term[..MaxSearchLength];
            }

            var specification = new ScholarshipsListSpecification(query, isActive, string.IsNullOrEmpty(term) ? null : term);

            var items = await _scholarships.ListAsync(specification, cancellationToken);
            var total = await _scholarships.CountAsync(specification, cancellationToken);

            return PagedResult<ScholarshipDto>.Create(items.Select(s => ReductionsMapper.ToDto(s)).ToList(), query, total);
        }

        public async Task<Result<ScholarshipDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<ScholarshipDto>.Fail(auth.Errors);
            }

            var scholarship = await _scholarships.FirstOrDefaultAsync(new ScholarshipByIdSpecification(id), cancellationToken);

            return scholarship is null ? FinanceErrors.ScholarshipNotFound : ReductionsMapper.ToDto(scholarship);
        }
    }






}
