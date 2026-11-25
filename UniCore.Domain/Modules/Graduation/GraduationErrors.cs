using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Graduation
{
    public static class GraduationErrors
    {
        public static readonly Error StudentNotFound = Error.NotFound("GRADUATION_STUDENT_NOT_FOUND", "The student was not found.");
        public static readonly Error ApplicationNotFound = Error.NotFound("GRADUATION_APPLICATION_NOT_FOUND", "The graduation application was not found.");
        public static readonly Error Invalid = Error.Validation("GRADUATION_INVALID", "The graduation data is not valid.");
        public static readonly Error ReasonRequired = Error.Validation("GRADUATION_REASON_REQUIRED", "A reason is required to reject an application.");
        public static readonly Error AlreadyApplied = Error.Conflict("GRADUATION_ALREADY_APPLIED", "The student already has an active or approved graduation application.");
        public static readonly Error InvalidState = Error.BusinessRule("GRADUATION_INVALID_STATE", "The application is not in a state that allows this change.");
        public static readonly Error NotEligible = Error.BusinessRule("GRADUATION_NOT_ELIGIBLE", "The student does not meet the graduation requirements.");
        public static readonly Error ActorRequired = Error.Forbidden("GRADUATION_ACTOR_REQUIRED", "A signed-in user is required.");

        // One code per missing requirement, returned by the eligibility check (not just true or false).
        public static readonly Error StudentNotActive = Error.BusinessRule("GRADUATION_STUDENT_NOT_ACTIVE", "Only an active student with a current program can graduate.");
        public static readonly Error RequiredCoursesMissing = Error.BusinessRule("GRADUATION_REQUIRED_COURSES_MISSING", "Required courses of the study plan have not been passed.");
        public static readonly Error CreditsInsufficient = Error.BusinessRule("GRADUATION_CREDITS_INSUFFICIENT", "The earned credits are below the credits the program requires.");
        public static readonly Error GpaBelowMinimum = Error.BusinessRule("GRADUATION_GPA_BELOW_MINIMUM", "The cumulative GPA is below the minimum for graduation.");
    }
}
