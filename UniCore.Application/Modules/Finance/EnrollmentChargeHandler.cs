using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance.Specifications;
using UniCore.Application.Modules.Registrations;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Finance
{
    // Finance reacts to the registration events. A per-credit fee (FeeBasis.PerCredit) of the student's active fee structure
    // becomes one charge per enrollment: credits x price. A semester invoice picks these charges up like any other.
    //  - Taking a seat creates the charge (idempotent: one live charge per enrollment and fee).
    //  - Giving the seat up cancels the charge while it is still Pending. A charge that is already on an invoice is NOT touched
    //    (the invoice may be paid); it is written to the audit trail as ENROLLMENT_CHARGE_NEEDS_REVIEW for the finance office.
    // The outbox delivers at least once and not in order, so the enrollment's CURRENT status decides, not the event.
    public sealed class EnrollmentChargeHandler(IAggregateRepository<Student> students, IAggregateRepository<FeeStructure> structures, IAggregateRepository<FeeDefinition> definitions, IAggregateRepository<StudentCharge> charges, IEnrollmentReadService enrollments, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IAggregateRepository<Student> _students = students;
        private readonly IAggregateRepository<FeeStructure> _structures = structures;
        private readonly IAggregateRepository<FeeDefinition> _definitions = definitions;
        private readonly IAggregateRepository<StudentCharge> _charges = charges;
        private readonly IEnrollmentReadService _enrollments = enrollments;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result> ChargeAsync(EnrollmentCreated seat, CancellationToken cancellationToken = default)
        {
            var current = await _enrollments.GetStudentEnrollmentsAsync(seat.StudentId, seat.AcademicSemesterId, cancellationToken);
            if (current.IsFailure)
            {
                return Result.Fail(current.Errors);
            }

            // A late delivery of an old event: the seat was given up since, so there is nothing to charge.
            if (!current.Value.Any(e => e.EnrollmentId == seat.EnrollmentId && e.Status is EnrollmentStatus.Registered or EnrollmentStatus.Completed))
            {
                return Result.Ok();
            }

            if (seat.Credits <= 0)
            {
                return Result.Ok();
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(seat.StudentId), cancellationToken);
            var program = student?.CurrentProgram;

            if (program is null)
            {
                return Result.Ok();
            }

            var active = await _structures.ListAsync(new ActiveFeeStructuresForGenerationSpecification(seat.AcademicSemesterId, program.AcademicProgramId), cancellationToken);
            var structure = ChargeHandler.Pick(active, program.AcademicProgramId, seat.AcademicSemesterId);

            if (structure is null || structure.Items.Count == 0)
            {
                return Result.Ok();
            }

            var definitionIds = structure.Items.Select(i => i.FeeDefinitionId).Distinct().ToArray();
            var found = await _definitions.ListAsync(new FeeDefinitionsByIdsSpecification(definitionIds), cancellationToken);
            var perCredit = found.Where(d => d.Basis == FeeBasis.PerCredit).ToDictionary(d => d.Id);

            if (perCredit.Count == 0)
            {
                return Result.Ok();
            }

            var existing = await _charges.ListAsync(new LiveChargesForEnrollmentSpecification(seat.EnrollmentId), cancellationToken);
            var charged = existing.Select(c => c.FeeDefinitionId).ToHashSet();

            var dueDate = _clock.UtcToday.AddDays(StudentChargeService.DefaultDueDays);
            var created = 0;

            foreach (var item in structure.Items.Where(i => perCredit.ContainsKey(i.FeeDefinitionId) && !charged.Contains(i.FeeDefinitionId)))
            {
                var built = StudentCharge.CreateForEnrollment(seat.StudentId, seat.AcademicSemesterId, item.Id, item.FeeDefinitionId, seat.EnrollmentId, seat.Credits, perCredit[item.FeeDefinitionId].Name, item.Amount, dueDate, _clock.UtcNow);
                if (!built.TryGetValue(out var charge))
                {
                    return Result.Fail(built.Errors);
                }

                _charges.Add(charge);
                created++;
            }

            if (created == 0)
            {
                return Result.Ok();
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);

            // The same event handled twice at the same time: the unique index let the other one win, which is the goal.
            return saved.IsSuccess || saved.Error?.Code == CommonErrors.DuplicateResource.Code ? Result.Ok() : Result.Fail(saved.Errors);
        }

        public async Task<Result> ReleaseAsync(EnrollmentDropped seat, CancellationToken cancellationToken = default)
        {
            var current = await _enrollments.GetStudentEnrollmentsAsync(seat.StudentId, seat.AcademicSemesterId, cancellationToken);
            if (current.IsFailure)
            {
                return Result.Fail(current.Errors);
            }

            // The student took the seat again before this (late) event arrived: the new charge is right, keep it.
            if (current.Value.Any(e => e.EnrollmentId == seat.EnrollmentId && e.Status is EnrollmentStatus.Registered or EnrollmentStatus.Completed))
            {
                return Result.Ok();
            }

            var live = await _charges.ListAsync(new LiveChargesForEnrollmentSpecification(seat.EnrollmentId, tracking: true), cancellationToken);
            var now = _clock.UtcNow;
            var cancelled = 0;
            var invoiced = new List<StudentCharge>();

            foreach (var charge in live)
            {
                if (charge.Status == ChargeStatus.Pending)
                {
                    var result = charge.Cancel(now);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    cancelled++;
                }
                else
                {
                    invoiced.Add(charge);
                }
            }

            if (cancelled > 0)
            {
                var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
                if (saved.IsFailure)
                {
                    return Result.Fail(saved.Errors);
                }
            }

            foreach (var charge in invoiced)
            {
                await _audit.WriteAsync(new AuditEntry(AuditActions.EnrollmentChargeNeedsReview, null, "StudentCharge", charge.Id.ToString(), NewValues: $"student {seat.StudentId} enrollment {seat.EnrollmentId} dropped; the charge is on invoice {charge.InvoiceId}"), cancellationToken);
            }

            return Result.Ok();
        }
    }

    public sealed class EnrollmentCreatedChargeHandler(EnrollmentChargeHandler handler) : IDomainEventHandler<EnrollmentCreated>
    {
        private readonly EnrollmentChargeHandler _handler = handler;

        public Task<Result> HandleAsync(EnrollmentCreated domainEvent, CancellationToken cancellationToken = default) => _handler.ChargeAsync(domainEvent, cancellationToken);
    }

    public sealed class EnrollmentDroppedChargeHandler(EnrollmentChargeHandler handler) : IDomainEventHandler<EnrollmentDropped>
    {
        private readonly EnrollmentChargeHandler _handler = handler;

        public Task<Result> HandleAsync(EnrollmentDropped domainEvent, CancellationToken cancellationToken = default) => _handler.ReleaseAsync(domainEvent, cancellationToken);
    }
}
