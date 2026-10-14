using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Finance
{
    public sealed class ChargeHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<StudentCharge> charges, IAggregateRepository<Student> students, IAggregateRepository<FeeStructure> structures, IAggregateRepository<FeeDefinition> definitions, IFinanceReferenceVerifier verifier, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        // PaginationQuery caps the page size at 100.
        private const int BatchSize = 100;

        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<StudentCharge> _charges = charges;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly IAggregateRepository<FeeStructure> _structures = structures;
        private readonly IAggregateRepository<FeeDefinition> _definitions = definitions;
        private readonly IFinanceReferenceVerifier _verifier = verifier;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task<Result> RequireAsync(string permission, CancellationToken cancellationToken) => _authorizer.AuthorizeAsync(permission, GlobalResource, cancellationToken);

        private Task AuditAsync(string action, string entityId, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken) => _audit.WriteAsync(
            new AuditEntry(action, _user.UserId, "StudentCharge", entityId, request.IpAddress, request.UserAgent, oldValues, newValues),
            cancellationToken);

        // Most specific first: program + semester, program only, semester only, global.
        private static FeeStructure? Pick(IReadOnlyList<FeeStructure> structures, Guid programId, Guid semesterId)
            => structures.FirstOrDefault(s => s.AcademicProgramId == programId && s.AcademicSemesterId == semesterId)
            ?? structures.FirstOrDefault(s => s.AcademicProgramId == programId && s.AcademicSemesterId == null)
            ?? structures.FirstOrDefault(s => s.AcademicProgramId == null && s.AcademicSemesterId == semesterId)
            ?? structures.FirstOrDefault(s => s.AcademicProgramId == null && s.AcademicSemesterId == null);

        // ---------- generate ----------

        // Safe to run again: a student never gets the same fee twice for the same semester.
        public async Task<Result<ChargeGenerationDto>> GenerateAsync(GenerateChargesCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Invoices.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<ChargeGenerationDto>.Fail(auth.Errors);
            }

            if (command.AcademicSemesterId is not { } semesterId || semesterId == Guid.Empty
                || (command.AcademicProgramId is { } programFilter && programFilter == Guid.Empty))
            {
                return FinanceErrors.ChargeDataInvalid;
            }

            if (command.DueDate < _clock.UtcToday)
            {
                return FinanceErrors.ChargeDueDateInvalid;
            }

            var semester = await _verifier.VerifySemesterAsync(semesterId, cancellationToken);
            if (semester.IsFailure)
            {
                return Result<ChargeGenerationDto>.Fail(semester.Errors);
            }

            if (command.AcademicProgramId is { } programId)
            {
                var program = await _verifier.VerifyProgramAsync(programId, cancellationToken);
                if (program.IsFailure)
                {
                    return Result<ChargeGenerationDto>.Fail(program.Errors);
                }
            }

            var activeStructures = await _structures.ListAsync(new ActiveFeeStructuresForGenerationSpecification(semesterId, command.AcademicProgramId), cancellationToken);

            var definitionIds = activeStructures.SelectMany(s => s.Items).Select(i => i.FeeDefinitionId).Distinct().ToArray();
            var definitionList = definitionIds.Length == 0
                ? []
                : await _definitions.ListAsync(new FeeDefinitionsByIdsSpecification(definitionIds), cancellationToken);
            var names = definitionList.ToDictionary(d => d.Id, d => d.Name);

            var processed = 0;
            var withoutStructure = 0;
            var created = 0;
            var alreadyExisting = 0;
            var now = _clock.UtcNow;

            var page = 1;

            while (true)
            {
                var query = new PaginationQuery(page, BatchSize);
                var batch = await _students.ListAsync(new StudentsForChargesSpecification(query, command.AcademicProgramId), cancellationToken);

                if (batch.Count == 0)
                {
                    break;
                }

                var existing = await _charges.ListAsync(new ChargesForStudentsSemesterSpecification(semesterId, batch.Select(s => s.Id).ToArray()), cancellationToken);
                var existingKeys = existing.Select(c => (c.StudentId, c.FeeDefinitionId)).ToHashSet();

                foreach (var student in batch)
                {
                    processed++;

                    var current = student.CurrentProgram;
                    var structure = current is null ? null : Pick(activeStructures, current.AcademicProgramId, semesterId);

                    if (structure is null)
                    {
                        withoutStructure++;
                        continue;
                    }

                    foreach (var item in structure.Items)
                    {
                        if (existingKeys.Contains((student.Id, item.FeeDefinitionId)))
                        {
                            alreadyExisting++;
                            continue;
                        }

                        var description = names.TryGetValue(item.FeeDefinitionId, out var name) ? name : "Fee";

                        var built = StudentCharge.Create(student.Id, semesterId, item.Id, item.FeeDefinitionId, description, item.Amount, command.DueDate, now);
                        if (!built.TryGetValue(out var charge))
                        {
                            return Result<ChargeGenerationDto>.Fail(built.Errors);
                        }

                        _charges.Add(charge);
                        created++;
                    }
                }

                var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
                if (saved.IsFailure)
                {
                    return saved.Error?.Code == CommonErrors.DuplicateResource.Code
                        ? FinanceErrors.ChargeGenerationConflict
                        : Result<ChargeGenerationDto>.Fail(saved.Errors);
                }

                if (batch.Count < BatchSize)
                {
                    break;
                }

                page++;
            }

            await AuditAsync(AuditActions.ChargesGenerated, semesterId.ToString(), request, null,
                $"students {processed}, created {created}, existing {alreadyExisting}, no structure {withoutStructure}", cancellationToken);

            return new ChargeGenerationDto(processed, withoutStructure, created, alreadyExisting);
        }

        // ---------- cancel ----------

        public async Task<Result<StudentChargeDto>> CancelAsync(Guid chargeId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Invoices.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<StudentChargeDto>.Fail(auth.Errors);
            }

            var charge = await _charges.FirstOrDefaultAsync(new StudentChargeByIdSpecification(chargeId, tracking: true), cancellationToken);
            if (charge is null)
            {
                return FinanceErrors.ChargeNotFound;
            }

            var cancelled = charge.Cancel(_clock.UtcNow);
            if (cancelled.IsFailure)
            {
                return Result<StudentChargeDto>.Fail(cancelled.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<StudentChargeDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.StudentChargeCancelled, charge.Id.ToString(), request, ChargeStatus.Pending.ToString(), ChargeStatus.Cancelled.ToString(), cancellationToken);

            return InvoicingMapper.ToDto(charge);
        }

        // ---------- queries ----------

        public async Task<Result<PagedResult<StudentChargeDto>>> ListAsync(PaginationQuery query, Guid? studentId, Guid? academicSemesterId, ChargeStatus? status, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<StudentChargeDto>>.Fail(auth.Errors);
            }

            var specification = new StudentChargesListSpecification(query, studentId, academicSemesterId, status);

            var items = await _charges.ListAsync(specification, cancellationToken);
            var total = await _charges.CountAsync(specification, cancellationToken);

            return PagedResult<StudentChargeDto>.Create(items.Select(c => InvoicingMapper.ToDto(c)).ToList(), query, total);
        }

        public async Task<Result<StudentChargeDto>> GetAsync(Guid chargeId, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<StudentChargeDto>.Fail(auth.Errors);
            }

            var charge = await _charges.FirstOrDefaultAsync(new StudentChargeByIdSpecification(chargeId), cancellationToken);

            return charge is null ? FinanceErrors.ChargeNotFound : InvoicingMapper.ToDto(charge);
        }
    }



}
