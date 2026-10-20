using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Admissions
{
    // Error codes of the Admissions module. Codes marked (doc) come from academic-lifecycle.md section 6.
    public static class AdmissionsErrors
    {
        // Applicant fields.
        public static readonly Error InvalidApplicantName = Error.Validation("APPLICANT_NAME_INVALID", "The applicant name is required and must be 2 to 200 characters.");
        public static readonly Error InvalidApplicantEmail = Error.Validation("APPLICANT_EMAIL_INVALID", "The applicant email address is not valid.");
        public static readonly Error InvalidApplicantPhone = Error.Validation("APPLICANT_PHONE_INVALID", "The applicant phone number is not valid.");
        public static readonly Error InvalidDateOfBirth = Error.Validation("APPLICANT_DATE_OF_BIRTH_INVALID", "The date of birth must be in the past and not more than 100 years ago.");
        public static readonly Error ApplicantNotFound = Error.NotFound("APPLICANT_NOT_FOUND", "The applicant was not found.");
        public static readonly Error ApplicantEmailExists = Error.Conflict("APPLICANT_EMAIL_EXISTS", "An applicant with this email already exists.");

        // Requirement catalog.
        public static readonly Error InvalidRequirementName = Error.Validation("ADMISSION_REQUIREMENT_NAME_INVALID", "The requirement name is required and must be 2 to 200 characters.");
        public static readonly Error InvalidRequirementCode = Error.Validation("ADMISSION_REQUIREMENT_CODE_INVALID", "The requirement code is required, 2 to 20 characters, using only letters, digits, '-' and '_'.");
        public static readonly Error InvalidRequirementDescription = Error.Validation("ADMISSION_REQUIREMENT_DESCRIPTION_INVALID", "The requirement description must be at most 1000 characters.");
        public static readonly Error RequirementNotFound = Error.NotFound("ADMISSION_REQUIREMENT_NOT_FOUND", "The admission requirement was not found.");
        public static readonly Error RequirementCodeExists = Error.Conflict("ADMISSION_REQUIREMENT_CODE_EXISTS", "A requirement with this code already exists in the program.");

        public static readonly Error AlreadyActive = Error.BusinessRule("ADMISSION_REQUIREMENT_ALREADY_ACTIVE", "The requirement is already active.");
        public static readonly Error AlreadyInactive = Error.BusinessRule("ADMISSION_REQUIREMENT_ALREADY_INACTIVE", "The requirement is already inactive.");

        // Application: input and lookups.
        public static readonly Error InvalidReference = Error.Validation("ADMISSION_REFERENCE_INVALID", "The applicant, program and academic year ids are required.");
        public static readonly Error ApplicationNotFound = Error.NotFound("ADMISSION_APPLICATION_NOT_FOUND", "The admission application was not found.");
        public static readonly Error ApplicationRequirementNotFound = Error.NotFound("ADMISSION_APPLICATION_REQUIREMENT_NOT_FOUND", "The requirement is not part of this application.");
        public static readonly Error DeadlineNotConfigured = Error.BusinessRule("ADMISSION_DEADLINE_NOT_CONFIGURED", "No admission deadline is configured for this academic year.");
        public static readonly Error DocumentInvalid = Error.Validation("ADMISSION_DOCUMENT_INVALID", "The document id is not valid.");
        public static readonly Error DocumentNotFound = Error.NotFound("ADMISSION_DOCUMENT_NOT_FOUND", "The document was not found.");

        // Application: rules (doc).
        public static readonly Error DuplicateApplication = Error.Conflict("ADMISSION_DUPLICATE_APPLICATION", "The applicant already has an active application for this program and academic year.");
        public static readonly Error InvalidState = Error.BusinessRule("ADMISSION_INVALID_STATE", "The application is not in a state that allows this action.");
        public static readonly Error Expired = Error.BusinessRule("ADMISSION_EXPIRED", "The admission deadline has passed.");
        public static readonly Error NotExpiredYet = Error.BusinessRule("ADMISSION_NOT_EXPIRED_YET", "The admission deadline has not passed yet.");
        public static readonly Error MandatoryRequirementMissing = Error.BusinessRule("ADMISSION_MANDATORY_REQUIREMENT_MISSING", "Every mandatory requirement needs a document before the application can be submitted.");
        public static readonly Error MandatoryRequirementNotApproved = Error.BusinessRule("ADMISSION_MANDATORY_REQUIREMENT_NOT_APPROVED", "Every mandatory requirement must be approved first.");
        public static readonly Error NoConditionsPending = Error.BusinessRule("ADMISSION_NO_CONDITIONS_PENDING", "All mandatory requirements are approved, so the application cannot be conditionally accepted.");
        public static readonly Error ConditionsPending = Error.BusinessRule("ADMISSION_CONDITIONS_PENDING", "Some mandatory requirements are still not approved.");
        public static readonly Error RequirementNotReviewable = Error.BusinessRule("ADMISSION_REQUIREMENT_NOT_REVIEWABLE", "The requirement has no document to review.");
        public static readonly Error ReviewDecisionRequired = Error.Validation("ADMISSION_REVIEW_DECISION_REQUIRED", "Approve or reject must be specified.");
        public static readonly Error ReviewNoteRequired = Error.Validation("ADMISSION_REVIEW_NOTE_REQUIRED", "A note is required when a requirement is rejected.");
        public static readonly Error ReasonRequired = Error.Validation("ADMISSION_REASON_REQUIRED", "A reason is required for this decision.");
        public static readonly Error ReasonTooLong = Error.Validation("ADMISSION_REASON_TOO_LONG", "The reason or note must be at most 1000 characters.");
        public static readonly Error ProgramInactive = Error.BusinessRule("ADMISSION_PROGRAM_INACTIVE", "Applications are not accepted for an inactive program.");
        public static readonly Error ProgramNotFound = Error.NotFound("ADMISSION_PROGRAM_NOT_FOUND", "The academic program was not found.");
    }
}
