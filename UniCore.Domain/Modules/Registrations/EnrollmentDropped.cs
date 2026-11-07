using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Registrations
{
    // Raised when a seat is given up: a drop, a withdrawal, or a cancelled registration (IsWithdrawal is false then).
    // Finance reacts to it (Developer 2). Consumers must be idempotent, keyed by EnrollmentId and EventId.
    public sealed record EnrollmentDropped(Guid EventId, DateTimeOffset OccurredAt, Guid RegistrationId, Guid EnrollmentId, Guid StudentId, Guid CourseSectionId, Guid AcademicSemesterId, int Credits, bool IsWithdrawal) : IDomainEvent;
}
