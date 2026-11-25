using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Registrations;

namespace UniCore.Application.Modules.Grading
{
    // N-34: when an enrollment is dropped or withdrawn (a student's drop, an approved withdrawal), its grade that is not final is removed.
    // Delivered at least once, so repeating it is safe: the second time there is no grade.
    public sealed class OrphanGradeHandler(IOrphanGradeService orphans, IUnitOfWork unitOfWork, IAuditWriter audit) : IDomainEventHandler<EnrollmentDropped>
    {
        public const string AuditAction = "GRADE_ORPHAN_REMOVED";

        private readonly IOrphanGradeService _orphans = orphans;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result> HandleAsync(EnrollmentDropped domainEvent, CancellationToken cancellationToken = default)
        {
            var removed = await _orphans.CancelForEnrollmentAsync(domainEvent.EnrollmentId, cancellationToken);
            if (removed.IsFailure)
            {
                return Result.Fail(removed.Errors);
            }

            if (removed.Value is not { } gradeId)
            {
                return Result.Ok();
            }

            var entry = new AuditEntry(AuditAction, null, "Grade", gradeId.ToString(), null, null, $"EnrollmentId={domainEvent.EnrollmentId}", "Removed");

            return await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
        }
    }
}
