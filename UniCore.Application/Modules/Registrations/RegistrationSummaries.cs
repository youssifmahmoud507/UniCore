using UniCore.Domain.Modules.Registrations;

namespace UniCore.Application.Modules.Registrations
{
    public sealed record EnrollmentSummary(
        Guid Id, Guid RegistrationId, Guid CourseSectionId, Guid CourseId, Guid CourseVersionId, int Credits, EnrollmentStatus Status,
        DateTimeOffset RegisteredAt, DateTimeOffset? DroppedAt, DateTimeOffset? WithdrawnAt, DateTimeOffset? CompletedAt);

    public sealed record RegistrationSummary(
        Guid Id, Guid StudentId, Guid AcademicSemesterId, RegistrationStatus Status, int RegisteredCredits,
        DateTimeOffset CreatedAt, DateTimeOffset? SubmittedAt, DateTimeOffset? CompletedAt, DateTimeOffset? CancelledAt,
        IReadOnlyList<EnrollmentSummary> Enrollments);

    // IsReplay: the same Idempotency-Key was already processed; nothing changed.
    public sealed record RegisterOutcome(RegistrationSummary Registration, EnrollmentSummary Enrollment, bool IsReplay);

    public static class RegistrationMapper
    {
        public static EnrollmentSummary ToSummary(Enrollment e)
            => new(e.Id, e.RegistrationId, e.CourseSectionId, e.CourseId, e.CourseVersionId, e.Credits, e.Status, e.RegisteredAt, e.DroppedAt, e.WithdrawnAt, e.CompletedAt);

        public static RegistrationSummary ToSummary(Registration r)
            => new(r.Id, r.StudentId, r.AcademicSemesterId, r.Status, r.RegisteredCredits, r.CreatedAt, r.SubmittedAt, r.CompletedAt, r.CancelledAt,
                [.. r.Enrollments.OrderBy(e => e.RegisteredAt).Select(e => ToSummary(e))]);
    }
}
