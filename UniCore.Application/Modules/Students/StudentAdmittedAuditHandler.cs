using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Audit;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Students
{
    // First real consumer of the outbox. Idempotent: one STUDENT_ADMITTED row per student.
    public sealed class StudentAdmittedAuditHandler(IAuditWriter audit, IAuditLogReader reader) : IDomainEventHandler<StudentAdmitted>
    {
        private readonly IAuditWriter _audit = audit;
        private readonly IAuditLogReader _reader = reader;

        public async Task<Result> HandleAsync(StudentAdmitted domainEvent, CancellationToken cancellationToken = default)
        {
            var entityId = domainEvent.StudentId.ToString();

            var existing = await _reader.ExistsAsync(AuditActions.StudentAdmitted, entityId, cancellationToken);

            if (!existing.TryGetValue(out var alreadyAudited))
            {
                return Result.Fail(existing.Errors);
            }

            if (alreadyAudited)
            {
                return Result.Ok();
            }

            await _audit.WriteAsync(new AuditEntry(AuditActions.StudentAdmitted, null, "Student", entityId, NewValues: domainEvent.StudentNumber), cancellationToken);

            return Result.Ok();
        }
    }
}
