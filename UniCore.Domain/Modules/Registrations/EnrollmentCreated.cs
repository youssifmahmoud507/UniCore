using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Registrations
{
    // Raised when a seat is taken (a new enrollment, or a dropped one re-registered). The outbox delivers it after the
    // save. Consumers (finance charges, notifications) must be idempotent, keyed by EnrollmentId and EventId.
    public sealed record EnrollmentCreated(Guid EventId, DateTimeOffset OccurredAt, Guid RegistrationId, Guid EnrollmentId, Guid StudentId, Guid CourseSectionId, Guid AcademicSemesterId, int Credits) : IDomainEvent;
}
