using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance.Specifications;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Finance
{
    public sealed class StudentChargeService(IAggregateRepository<Student> students, IAggregateRepository<FeeDefinition> definitions, IAggregateRepository<StudentCharge> charges, IUnitOfWork unitOfWork, IClock clock) : IStudentChargeService
    {
        public const int DefaultDueDays = 14;

        private readonly IAggregateRepository<Student> _students = students;
        private readonly IAggregateRepository<FeeDefinition> _definitions = definitions;
        private readonly IAggregateRepository<StudentCharge> _charges = charges;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;

        public async Task<Result<ServiceChargeResult>> ChargeAsync(ServiceChargeRequest request, CancellationToken cancellationToken = default)
        {
            var code = FeeDefinition.NormalizeCode(request.FeeCode);
            if (code is null)
            {
                return FinanceErrors.FeeCodeInvalid;
            }

            var reference = request.Reference?.Trim();
            if (string.IsNullOrEmpty(reference) || reference.Length > StudentCharge.MaxReferenceLength)
            {
                return FinanceErrors.ServiceReferenceInvalid;
            }

            if (request.StudentId == Guid.Empty)
            {
                return FinanceErrors.ChargeDataInvalid;
            }

            var today = _clock.UtcToday;

            if (request.DueDate is { } requested && requested < today)
            {
                return FinanceErrors.ChargeDueDateInvalid;
            }

            // A retried call: the same reference gives back the same charge.
            var existing = await _charges.FirstOrDefaultAsync(new ServiceChargeByReferenceSpecification(request.StudentId, reference), cancellationToken);
            if (existing is not null)
            {
                return new ServiceChargeResult(InvoicingMapper.ToDto(existing), false);
            }

            if (!await _students.AnyAsync(new StudentByIdSpecification(request.StudentId), cancellationToken))
            {
                return StudentErrors.NotFound;
            }

            var found = await _definitions.ListAsync(new FeeDefinitionsByCodeSpecification(code), cancellationToken);

            if (found.Count == 0)
            {
                return FinanceErrors.FeeDefinitionNotFound;
            }

            if (found.Count > 1)
            {
                return FinanceErrors.FeeCodeAmbiguous;
            }

            var definition = found[0];

            if (!definition.IsActive)
            {
                return FinanceErrors.FeeDefinitionInactive;
            }

            var amount = definition.DefaultAmount;

            if (request.Amount is { } custom)
            {
                var parsed = Money.Of(custom);
                if (!parsed.TryGetValue(out amount))
                {
                    return Result<ServiceChargeResult>.Fail(parsed.Errors);
                }
            }

            var dueDate = request.DueDate ?? today.AddDays(DefaultDueDays);

            var built = StudentCharge.CreateService(request.StudentId, definition.Id, definition.Name, amount, dueDate, reference, _clock.UtcNow);
            if (!built.TryGetValue(out var charge))
            {
                return Result<ServiceChargeResult>.Fail(built.Errors);
            }

            _charges.Add(charge);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                // Two calls with the same reference at the same time: the unique index lets one win. Return the winner.
                if (saved.Error?.Code == CommonErrors.DuplicateResource.Code)
                {
                    var winner = await _charges.FirstOrDefaultAsync(new ServiceChargeByReferenceSpecification(request.StudentId, reference), cancellationToken);
                    if (winner is not null)
                    {
                        return new ServiceChargeResult(InvoicingMapper.ToDto(winner), false);
                    }
                }

                return Result<ServiceChargeResult>.Fail(saved.Errors);
            }

            return new ServiceChargeResult(InvoicingMapper.ToDto(charge), true);
        }
    }

}
