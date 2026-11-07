using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Registrations
{
    // One course section inside a registration. Created and changed only through Registration.
    // CourseId and Credits are copied when the seat is taken, so the checks (already enrolled, credit limit) and the
    // history do not depend on later changes to the course. A Dropped row is reused when the student registers the
    // same section again (unique key RegistrationId + CourseSectionId).
    public sealed class Enrollment : BaseEntity
    {
        private Enrollment()
        {
        }

        public Guid RegistrationId { get; private set; }
        public Guid CourseSectionId { get; private set; }
        public Guid CourseId { get; private set; }
        public Guid CourseVersionId { get; private set; }
        public int Credits { get; private set; }
        public EnrollmentStatus Status { get; private set; }
        public DateTimeOffset RegisteredAt { get; private set; }
        public DateTimeOffset? DroppedAt { get; private set; }
        public DateTimeOffset? WithdrawnAt { get; private set; }
        public DateTimeOffset? CompletedAt { get; private set; }

        // The Idempotency-Key of the request that took the seat (unique inside the registration).
        public string? IdempotencyKey { get; private set; }

        public bool HoldsSeat => Status == EnrollmentStatus.Registered;

        internal static Enrollment Create(Guid registrationId, Guid courseSectionId, Guid courseId, Guid courseVersionId, int credits, string? idempotencyKey, DateTimeOffset now)
            => new()
            {
                Id = Guid.NewGuid(),
                RegistrationId = registrationId,
                CourseSectionId = courseSectionId,
                CourseId = courseId,
                CourseVersionId = courseVersionId,
                Credits = credits,
                Status = EnrollmentStatus.Registered,
                RegisteredAt = now,
                IdempotencyKey = idempotencyKey
            };

        // Dropped -> Registered again (same section).
        internal void Reactivate(Guid courseId, Guid courseVersionId, int credits, string? idempotencyKey, DateTimeOffset now)
        {
            CourseId = courseId;
            CourseVersionId = courseVersionId;
            Credits = credits;
            Status = EnrollmentStatus.Registered;
            RegisteredAt = now;
            DroppedAt = null;
            IdempotencyKey = idempotencyKey;
        }

        internal void MarkDropped(DateTimeOffset now)
        {
            Status = EnrollmentStatus.Dropped;
            DroppedAt = now;
        }

        internal void MarkWithdrawn(DateTimeOffset now)
        {
            Status = EnrollmentStatus.Withdrawn;
            WithdrawnAt = now;
        }

        internal void MarkCompleted(DateTimeOffset now)
        {
            Status = EnrollmentStatus.Completed;
            CompletedAt = now;
        }
    }
}
