using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Registrations
{
    // Error codes of the Registrations module. The pipeline codes are the ones in registration-rules.md section 2.
    public static class RegistrationsErrors
    {
        // Request.
        public static readonly Error RequestInvalid = Error.Validation("REGISTRATION_REQUEST_INVALID", "The student, the semester and the section are required.");
        public static readonly Error IdempotencyKeyInvalid = Error.Validation("IDEMPOTENCY_KEY_INVALID", "The Idempotency-Key header is required: 8 to 100 visible characters, no spaces.");
        public static readonly Error IdempotencyKeyReused = Error.Conflict("IDEMPOTENCY_KEY_REUSED", "This Idempotency-Key was already used for a different registration.");
        public static readonly Error OverrideReasonRequired = Error.Validation("REGISTRATION_OVERRIDE_REASON_INVALID", "The override reason is required, 5 to 500 characters.");
        public static readonly Error OverrideForbidden = Error.Forbidden("REGISTRATION_OVERRIDE_FORBIDDEN", "Only registration staff can register outside the registration window.");
        public static readonly Error EnrollmentsRequired = Error.Validation("REGISTRATION_ENROLLMENTS_REQUIRED", "At least one enrollment id is required.");

        // Registration.
        public static readonly Error RegistrationNotFound = Error.NotFound("REGISTRATION_NOT_FOUND", "The registration was not found.");
        public static readonly Error InvalidState = Error.BusinessRule("REGISTRATION_INVALID_STATE", "The registration is not in a state that allows this action.");
        public static readonly Error Empty = Error.BusinessRule("REGISTRATION_EMPTY", "The registration needs at least one registered course before it can be submitted.");
        public static readonly Error Closed = Error.BusinessRule("REGISTRATION_CLOSED", "Registration is not open for this semester.");
        public static readonly Error SemesterNotFound = Error.NotFound("REGISTRATION_SEMESTER_NOT_FOUND", "The semester was not found.");
        public static readonly Error SemesterClosed = Error.BusinessRule("REGISTRATION_SEMESTER_CLOSED", "The semester is closed.");

        // Pipeline (registration-rules.md section 2, in this order).
        public static readonly Error SectionNotFound = Error.NotFound("SECTION_NOT_FOUND", "The course section was not found.");
        public static readonly Error SectionNotOpen = Error.BusinessRule("SECTION_NOT_OPEN", "The section is not open for enrollment.");
        public static readonly Error SectionSemesterMismatch = Error.BusinessRule("SECTION_SEMESTER_MISMATCH", "The section is offered in another semester.");
        public static readonly Error StudentNotFound = Error.NotFound("REGISTRATION_STUDENT_NOT_FOUND", "The student was not found.");
        public static readonly Error StudentInactive = Error.BusinessRule("STUDENT_INACTIVE", "Only an active student can register, drop or withdraw.");
        public static readonly Error FinancialHold = Error.BusinessRule("FINANCIAL_HOLD", "A financial hold blocks registration.");
        public static readonly Error CourseNotInStudyPlan = Error.BusinessRule("COURSE_NOT_IN_STUDY_PLAN", "The course is not part of the student's study plan.");
        public static readonly Error PrerequisiteNotMet = Error.BusinessRule("PREREQUISITE_NOT_MET", "The prerequisites of the course are not met.");
        public static readonly Error CorequisiteNotMet = Error.BusinessRule("COREQUISITE_NOT_MET", "A corequisite course is neither passed nor registered this semester.");
        public static readonly Error CourseAlreadyPassed = Error.BusinessRule("COURSE_ALREADY_PASSED", "The student has already passed this course.");
        public static readonly Error AlreadyEnrolled = Error.Conflict("ALREADY_ENROLLED", "The student is already enrolled in this course this semester.");
        public static readonly Error CreditLimitExceeded = Error.BusinessRule("CREDIT_LIMIT_EXCEEDED", "The credit limit would be exceeded.");
        public static readonly Error ScheduleConflict = Error.BusinessRule("SCHEDULE_CONFLICT", "The section overlaps another section the student is enrolled in.");

        // Enrollment.
        public static readonly Error EnrollmentNotFound = Error.NotFound("ENROLLMENT_NOT_FOUND", "The enrollment was not found.");
        public static readonly Error EnrollmentInvalidState = Error.BusinessRule("ENROLLMENT_INVALID_STATE", "The enrollment is already dropped, withdrawn or completed.");
        public static readonly Error AddDropClosed = Error.BusinessRule("ADD_DROP_CLOSED", "The add/drop period is closed.");
        public static readonly Error WithdrawalDeadlinePassed = Error.BusinessRule("WITHDRAWAL_DEADLINE_PASSED", "Withdrawal is not open: the deadline has passed or add/drop is still open.");
        public static readonly Error CorequisiteDropBlocked = Error.BusinessRule("COREQUISITE_DROP_BLOCKED", "Another course the student is enrolled in needs this course as a corequisite.");
    }
}
