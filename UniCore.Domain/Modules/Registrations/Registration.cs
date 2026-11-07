using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Registrations
{
    // A student's registration for one semester (database index on StudentId + AcademicSemesterId). It is the aggregate
    // root of its enrollments: they are created and changed only here, and it has a RowVersion, so two requests changing
    // the same student's registration at once cannot both win (registration-rules.md section 6).
    // The pipeline of checks (student active, window, hold, plan, prerequisites, credits, schedule, capacity) runs in the
    // application layer BEFORE these methods; they keep only the rules of the aggregate itself. Seats are taken on the
    // CourseSection by the handler in the same save.
    public sealed class Registration : AggregateRoot
    {
        private readonly List<Enrollment> _enrollments = [];

        private Registration()
        {
        }

        public Guid StudentId { get; private set; }
        public Guid AcademicSemesterId { get; private set; }
        public RegistrationStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? SubmittedAt { get; private set; }
        public DateTimeOffset? CompletedAt { get; private set; }
        public DateTimeOffset? CancelledAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public IReadOnlyList<Enrollment> Enrollments => _enrollments.AsReadOnly();

        // Enrollments that hold a seat.
        public IEnumerable<Enrollment> RegisteredEnrollments => _enrollments.Where(e => e.HoldsSeat);

        public int RegisteredCredits => _enrollments.Where(e => e.HoldsSeat).Sum(e => e.Credits);

        public bool AcceptsChanges => Status is RegistrationStatus.Draft or RegistrationStatus.Submitted;

        public static Result<Registration> Create(Guid studentId, Guid academicSemesterId, DateTimeOffset now)
        {
            if (studentId == Guid.Empty || academicSemesterId == Guid.Empty)
            {
                return RegistrationsErrors.RequestInvalid;
            }

            return new Registration
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                AcademicSemesterId = academicSemesterId,
                Status = RegistrationStatus.Draft,
                CreatedAt = now
            };
        }

        // ---------- enrollments ----------

        // Takes the section for the student. A Dropped row of the same section is reactivated instead of duplicated;
        // Registered -> ALREADY_ENROLLED; Withdrawn or Completed -> ENROLLMENT_INVALID_STATE.
        public Result<Enrollment> Enroll(Guid courseSectionId, Guid courseId, Guid courseVersionId, int credits, string? idempotencyKey, DateTimeOffset now)
        {
            if (!AcceptsChanges)
            {
                return RegistrationsErrors.InvalidState;
            }

            if (courseSectionId == Guid.Empty || courseId == Guid.Empty || courseVersionId == Guid.Empty || credits < 1)
            {
                return RegistrationsErrors.RequestInvalid;
            }

            var existing = _enrollments.FirstOrDefault(e => e.CourseSectionId == courseSectionId);

            Enrollment enrollment;

            if (existing is null)
            {
                enrollment = Enrollment.Create(Id, courseSectionId, courseId, courseVersionId, credits, idempotencyKey, now);
                _enrollments.Add(enrollment);
            }
            else if (existing.Status == EnrollmentStatus.Dropped)
            {
                existing.Reactivate(courseId, courseVersionId, credits, idempotencyKey, now);
                enrollment = existing;
            }
            else if (existing.Status == EnrollmentStatus.Registered)
            {
                return RegistrationsErrors.AlreadyEnrolled;
            }
            else
            {
                return RegistrationsErrors.EnrollmentInvalidState;
            }

            UpdatedAt = now;
            RaiseDomainEvent(new EnrollmentCreated(Guid.NewGuid(), now, Id, enrollment.Id, StudentId, courseSectionId, AcademicSemesterId, credits));
            return enrollment;
        }

        // Registered -> Dropped. The handler checks the add/drop window and releases the seat.
        public Result<Enrollment> Drop(Guid enrollmentId, DateTimeOffset now) => Leave(enrollmentId, withdraw: false, now);

        // Registered -> Withdrawn. The handler checks the withdrawal window and releases the seat.
        public Result<Enrollment> Withdraw(Guid enrollmentId, DateTimeOffset now) => Leave(enrollmentId, withdraw: true, now);

        private Result<Enrollment> Leave(Guid enrollmentId, bool withdraw, DateTimeOffset now)
        {
            if (!AcceptsChanges)
            {
                return RegistrationsErrors.InvalidState;
            }

            var enrollment = _enrollments.FirstOrDefault(e => e.Id == enrollmentId);
            if (enrollment is null)
            {
                return RegistrationsErrors.EnrollmentNotFound;
            }

            if (!enrollment.HoldsSeat)
            {
                return RegistrationsErrors.EnrollmentInvalidState;
            }

            if (withdraw)
            {
                enrollment.MarkWithdrawn(now);
            }
            else
            {
                enrollment.MarkDropped(now);
            }

            UpdatedAt = now;
            RaiseDomainEvent(new EnrollmentDropped(Guid.NewGuid(), now, Id, enrollment.Id, StudentId, enrollment.CourseSectionId, AcademicSemesterId, enrollment.Credits, withdraw));
            return enrollment;
        }

        // ---------- lifecycle ----------

        // Draft -> Submitted. Needs at least one registered course. Add/drop stays possible afterwards.
        public Result Submit(DateTimeOffset now)
        {
            if (Status != RegistrationStatus.Draft)
            {
                return Result.Fail(RegistrationsErrors.InvalidState);
            }

            if (!_enrollments.Any(e => e.HoldsSeat))
            {
                return Result.Fail(RegistrationsErrors.Empty);
            }

            Status = RegistrationStatus.Submitted;
            SubmittedAt = now;
            UpdatedAt = now;
            return Result.Ok();
        }

        // Draft or Submitted -> Cancelled. Every registered enrollment becomes Dropped; they are returned so the
        // handler can release their seats.
        public Result<IReadOnlyList<Enrollment>> Cancel(DateTimeOffset now)
        {
            if (!AcceptsChanges)
            {
                return RegistrationsErrors.InvalidState;
            }

            var dropped = new List<Enrollment>();

            foreach (var enrollment in _enrollments.Where(e => e.HoldsSeat))
            {
                enrollment.MarkDropped(now);
                dropped.Add(enrollment);
                RaiseDomainEvent(new EnrollmentDropped(Guid.NewGuid(), now, Id, enrollment.Id, StudentId, enrollment.CourseSectionId, AcademicSemesterId, enrollment.Credits, false));
            }

            Status = RegistrationStatus.Cancelled;
            CancelledAt = now;
            UpdatedAt = now;
            return dropped;
        }

        // Draft or Submitted -> Completed, by the semester-close job. Registered enrollments become Completed.
        public Result Complete(DateTimeOffset now)
        {
            if (!AcceptsChanges)
            {
                return Result.Fail(RegistrationsErrors.InvalidState);
            }

            foreach (var enrollment in _enrollments.Where(e => e.HoldsSeat))
            {
                enrollment.MarkCompleted(now);
            }

            Status = RegistrationStatus.Completed;
            CompletedAt = now;
            UpdatedAt = now;
            return Result.Ok();
        }

        // ---------- questions ----------

        public bool HasRegisteredCourse(Guid courseId) => _enrollments.Any(e => e.HoldsSeat && e.CourseId == courseId);

        public Enrollment? FindByKey(string key) => _enrollments.FirstOrDefault(e => e.IdempotencyKey == key);

        // Valid key: 8 to 100 visible ASCII characters, no spaces. Null means invalid.
        public static string? NormalizeKey(string? key)
        {
            var clean = key?.Trim();

            if (string.IsNullOrEmpty(clean) || clean.Length < 8 || clean.Length > 100)
            {
                return null;
            }

            return clean.All(c => c is > ' ' and < '\u007f') ? clean : null;
        }
    }
}
