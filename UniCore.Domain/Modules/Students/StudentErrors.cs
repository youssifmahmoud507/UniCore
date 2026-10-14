using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Students
{
    public static class StudentErrors
    {
        public static readonly Error NotFound = Error.NotFound("STUDENT_NOT_FOUND", "The student was not found.");
        public static readonly Error DataInvalid = Error.Validation("STUDENT_DATA_INVALID", "The student data is invalid.");
        public static readonly Error NumberInvalid = Error.Validation("STUDENT_NUMBER_INVALID", "The student number is invalid.");
        public static readonly Error AlreadyExists = Error.Conflict("STUDENT_ALREADY_EXISTS", "A student record already exists for this person.");
        public static readonly Error StatusUnchanged = Error.BusinessRule("STUDENT_STATUS_UNCHANGED", "The student already has this status.");
        public static readonly Error StatusTransitionInvalid = Error.BusinessRule("STUDENT_STATUS_TRANSITION_INVALID", "This status change is not allowed.");
        public static readonly Error StatusReasonRequired = Error.Validation("STUDENT_STATUS_REASON_REQUIRED", "A reason is required for this status.");
        public static readonly Error EffectiveDateInvalid = Error.Validation("STUDENT_EFFECTIVE_DATE_INVALID", "The effective date is not valid.");
        public static readonly Error NotActive = Error.BusinessRule("STUDENT_NOT_ACTIVE", "The student is not active.");
        public static readonly Error ProgramUnchanged = Error.BusinessRule("STUDENT_PROGRAM_UNCHANGED", "The student is already in this program, plan and level.");
        public static readonly Error ProgramChangeReasonRequired = Error.Validation("STUDENT_PROGRAM_CHANGE_REASON_REQUIRED", "A reason is required to change the program.");
        public static readonly Error ProgramMissing = Error.Failure("STUDENT_PROGRAM_MISSING", "The student has no current program record.");
        public static readonly Error ProfileMissing = Error.Failure("STUDENT_PROFILE_MISSING", "The student has no academic profile.");
        public static readonly Error AccountAlreadyLinked = Error.Conflict("STUDENT_ACCOUNT_ALREADY_LINKED", "The student already has a different account.");
        public static readonly Error AccountConflict = Error.Conflict("STUDENT_ACCOUNT_CONFLICT", "An account with this student number already exists for another email.");
        public static readonly Error NumberGenerationFailed = Error.Failure("STUDENT_NUMBER_GENERATION_FAILED", "A student number could not be generated.");
    }
}
