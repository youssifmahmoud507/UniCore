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
    public sealed class FeeStructureHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<FeeStructure> structures, IAggregateRepository<FeeDefinition> definitions, IFinanceReferenceVerifier verifier, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<FeeStructure> _structures = structures;
        private readonly IAggregateRepository<FeeDefinition> _definitions = definitions;
        private readonly IFinanceReferenceVerifier _verifier = verifier;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task<Result> RequireAsync(string permission, CancellationToken cancellationToken) => _authorizer.AuthorizeAsync(permission, GlobalResource, cancellationToken);

        private Task AuditAsync(string action, Guid entityId, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken) => _audit.WriteAsync(
            new AuditEntry(action, _user.UserId, "FeeStructure", entityId.ToString(), request.IpAddress, request.UserAgent, oldValues, newValues),
            cancellationToken);

        private async Task<Result<FeeStructure>> LoadAsync(Guid id, CancellationToken cancellationToken)
        {
            var structure = await _structures.FirstOrDefaultAsync(new FeeStructureByIdSpecification(id, tracking: true), cancellationToken);

            return structure is null ? FinanceErrors.FeeStructureNotFound : structure;
        }

        // ---------- create ----------

        public async Task<Result<FeeStructureDto>> CreateAsync(CreateFeeStructureCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Fees.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(auth.Errors);
            }

            if (command.AcademicProgramId is { } programId)
            {
                var program = await _verifier.VerifyProgramAsync(programId, cancellationToken);
                if (program.IsFailure)
                {
                    return Result<FeeStructureDto>.Fail(program.Errors);
                }
            }

            if (command.AcademicSemesterId is { } semesterId)
            {
                var semester = await _verifier.VerifySemesterAsync(semesterId, cancellationToken);
                if (semester.IsFailure)
                {
                    return Result<FeeStructureDto>.Fail(semester.Errors);
                }
            }

            var created = FeeStructure.Create(command.AcademicProgramId, command.AcademicSemesterId, command.Name, _clock.UtcNow);
            if (!created.TryGetValue(out var structure))
            {
                return Result<FeeStructureDto>.Fail(created.Errors);
            }

            _structures.Add(structure);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.FeeStructureCreated, structure.Id, request, null, structure.Name, cancellationToken);

            return FinanceMapper.ToDto(structure);
        }

        // ---------- items ----------

        // amount = null means "use the fee definition's default amount".
        public async Task<Result<FeeStructureDto>> AddItemAsync(Guid structureId, Guid? feeDefinitionId, decimal? amount, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Fees.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(auth.Errors);
            }

            if (feeDefinitionId is not { } definitionId || definitionId == Guid.Empty)
            {
                return FinanceErrors.FeeStructureDataInvalid;
            }

            var definition = await _definitions.FirstOrDefaultAsync(new FeeDefinitionByIdSpecification(definitionId), cancellationToken);
            if (definition is null)
            {
                return FinanceErrors.FeeDefinitionNotFound;
            }

            if (!definition.IsActive)
            {
                return FinanceErrors.FeeDefinitionInactive;
            }

            var price = definition.DefaultAmount;

            if (amount is { } rawAmount)
            {
                var parsed = Money.Of(rawAmount);
                if (!parsed.TryGetValue(out var money))
                {
                    return Result<FeeStructureDto>.Fail(parsed.Errors);
                }

                price = money;
            }

            var loaded = await LoadAsync(structureId, cancellationToken);
            if (!loaded.TryGetValue(out var structure))
            {
                return Result<FeeStructureDto>.Fail(loaded.Errors);
            }

            var added = structure.AddItem(definition.Id, price, _clock.UtcNow);
            if (!added.TryGetValue(out var item))
            {
                return Result<FeeStructureDto>.Fail(added.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.FeeStructureItemAdded, structure.Id, request, null, $"{definition.Code} {item.Amount}", cancellationToken);

            return FinanceMapper.ToDto(structure);
        }

        public async Task<Result<FeeStructureDto>> ChangeItemAmountAsync(Guid structureId, Guid itemId, decimal? amount, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Fees.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(auth.Errors);
            }

            if (amount is not { } rawAmount)
            {
                return FinanceErrors.AmountRequired;
            }

            var parsed = Money.Of(rawAmount);
            if (!parsed.TryGetValue(out var money))
            {
                return Result<FeeStructureDto>.Fail(parsed.Errors);
            }

            var loaded = await LoadAsync(structureId, cancellationToken);
            if (!loaded.TryGetValue(out var structure))
            {
                return Result<FeeStructureDto>.Fail(loaded.Errors);
            }

            var oldAmount = structure.Items.FirstOrDefault(i => i.Id == itemId)?.Amount;

            var changed = structure.ChangeItemAmount(itemId, money, _clock.UtcNow);
            if (changed.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(changed.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.FeeStructureItemChanged, structure.Id, request, oldAmount?.ToString(), money.ToString(), cancellationToken);

            return FinanceMapper.ToDto(structure);
        }

        public async Task<Result<FeeStructureDto>> RemoveItemAsync(Guid structureId, Guid itemId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Fees.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(auth.Errors);
            }

            var loaded = await LoadAsync(structureId, cancellationToken);
            if (!loaded.TryGetValue(out var structure))
            {
                return Result<FeeStructureDto>.Fail(loaded.Errors);
            }

            var removed = structure.RemoveItem(itemId, _clock.UtcNow);
            if (removed.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(removed.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.FeeStructureItemRemoved, structure.Id, request, itemId.ToString(), null, cancellationToken);

            return FinanceMapper.ToDto(structure);
        }

        // ---------- lifecycle ----------

        public async Task<Result<FeeStructureDto>> ActivateAsync(Guid structureId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Fees.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(auth.Errors);
            }

            var loaded = await LoadAsync(structureId, cancellationToken);
            if (!loaded.TryGetValue(out var structure))
            {
                return Result<FeeStructureDto>.Fail(loaded.Errors);
            }

            // Friendly check first. The unique index below is the real guard against two parallel activations.
            if (structure.Status == FeeStructureStatus.Draft && await _structures.AnyAsync(new ActiveFeeStructureForScopeSpecification(structure.AcademicProgramId, structure.AcademicSemesterId, structure.Id), cancellationToken))
            {
                return FinanceErrors.FeeStructureScopeTaken;
            }

            var activated = structure.Activate(_clock.UtcNow);
            if (activated.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(activated.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error?.Code == CommonErrors.DuplicateResource.Code
                    ? FinanceErrors.FeeStructureScopeTaken
                    : Result<FeeStructureDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.FeeStructureActivated, structure.Id, request, FeeStructureStatus.Draft.ToString(), $"{FeeStructureStatus.Active} total {structure.Total}", cancellationToken);

            return FinanceMapper.ToDto(structure);
        }

        public async Task<Result<FeeStructureDto>> ArchiveAsync(Guid structureId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Fees.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(auth.Errors);
            }

            var loaded = await LoadAsync(structureId, cancellationToken);
            if (!loaded.TryGetValue(out var structure))
            {
                return Result<FeeStructureDto>.Fail(loaded.Errors);
            }

            var archived = structure.Archive(_clock.UtcNow);
            if (archived.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(archived.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.FeeStructureArchived, structure.Id, request, FeeStructureStatus.Active.ToString(), FeeStructureStatus.Archived.ToString(), cancellationToken);

            return FinanceMapper.ToDto(structure);
        }

        // ---------- queries ----------

        public async Task<Result<PagedResult<FeeStructureSummaryDto>>> ListAsync(PaginationQuery query, FeeStructureStatus? status, Guid? academicProgramId, Guid? academicSemesterId, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<FeeStructureSummaryDto>>.Fail(auth.Errors);
            }

            var specification = new FeeStructuresListSpecification(query, status, academicProgramId, academicSemesterId);

            var items = await _structures.ListAsync(specification, cancellationToken);
            var total = await _structures.CountAsync(specification, cancellationToken);

            return PagedResult<FeeStructureSummaryDto>.Create(items.Select(FinanceMapper.ToSummary).ToList(), query, total);
        }

        public async Task<Result<FeeStructureDto>> GetAsync(Guid structureId, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<FeeStructureDto>.Fail(auth.Errors);
            }

            var structure = await _structures.FirstOrDefaultAsync(new FeeStructureByIdSpecification(structureId), cancellationToken);

            return structure is null ? FinanceErrors.FeeStructureNotFound : FinanceMapper.ToDto(structure);
        }
    }

}
