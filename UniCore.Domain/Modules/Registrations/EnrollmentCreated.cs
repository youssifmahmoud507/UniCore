using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Registrations
{
    // Raised when a seat is taken (a new enrollment, or a dropped one re-registered). The outbox delivers it after the
    // save. Consumers (finance charges, notifications) must be idempotent, keyed by EnrollmentId and EventId.
    // The payload is a contract with Finance (Week 17): StudentId, CourseSectionId, CourseId, Credits and AcademicSemesterId stay,
    // and a field is only ever added, never removed or renamed.
    public sealed record EnrollmentCreated(Guid EventId, DateTimeOffset OccurredAt, Guid RegistrationId, Guid EnrollmentId, Guid StudentId, Guid CourseSectionId, Guid CourseId, Guid AcademicSemesterId, int Credits) : IDomainEvent;
}
