using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Admissions
{
    // Raised once, when an application becomes Accepted (directly, or after its conditions are completed).
    // The outbox delivers it after the application is saved. The Students side turns it into a student
    // (StudentAdmissionService.AdmitAsync) and must be idempotent, keyed by ApplicationId, because
    // delivery is at-least-once. The event is named after the application, not "StudentAdmitted":
    // that name already belongs to the event raised by Student.Create.
    public sealed record AdmissionApplicationAccepted(Guid EventId, DateTimeOffset OccurredAt, Guid ApplicationId, Guid ApplicantId, Guid AcademicProgramId, Guid AcademicYearId, Guid DecisionId) : IDomainEvent;
}
