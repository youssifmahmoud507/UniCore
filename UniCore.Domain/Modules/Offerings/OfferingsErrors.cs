using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Offerings
{
    // Error codes of the Offerings module (offerings, sections, instructor assignments).
    public static class OfferingsErrors
    {
        // Offerings.
        public static readonly Error InvalidParent = Error.Validation("OFFERING_PARENT_INVALID", "The course version id and the semester id are required.");
        public static readonly Error MaxSectionsInvalid = Error.Validation("OFFERING_MAX_SECTIONS_INVALID", "The maximum number of sections must be between 1 and 50, or empty for no limit.");
        public static readonly Error OfferingNotFound = Error.NotFound("OFFERING_NOT_FOUND", "The course offering was not found.");
        public static readonly Error OfferingExists = Error.Conflict("OFFERING_EXISTS", "The course version is already offered in this semester.");
        public static readonly Error OfferingInvalidState = Error.BusinessRule("OFFERING_INVALID_STATE", "The offering is not in a state that allows this action.");
        public static readonly Error OfferingNeedsSection = Error.BusinessRule("OFFERING_NEEDS_SECTION", "An offering needs at least one open section before it can be opened.");
        public static readonly Error SemesterNotFound = Error.NotFound("OFFERING_SEMESTER_NOT_FOUND", "The semester was not found.");
        public static readonly Error SemesterClosed = Error.BusinessRule("OFFERING_SEMESTER_CLOSED", "The semester is closed, so its offerings cannot change.");
        public static readonly Error CourseVersionNotFound = Error.NotFound("OFFERING_COURSE_VERSION_NOT_FOUND", "The course version was not found.");
        public static readonly Error CourseVersionNotEffective = Error.BusinessRule("OFFERING_COURSE_VERSION_NOT_EFFECTIVE", "The course version is not effective at the start of the semester.");
        public static readonly Error MaxSectionsReached = Error.BusinessRule("OFFERING_MAX_SECTIONS_REACHED", "The offering already has the maximum number of sections.");

        // Sections.
        public static readonly Error SectionCodeInvalid = Error.Validation("SECTION_CODE_INVALID", "The section code is required, 1 to 10 characters, using only letters, digits, '-' and '_'.");
        public static readonly Error CapacityInvalid = Error.Validation("SECTION_CAPACITY_INVALID", "The capacity must be between 1 and 1000.");
        public static readonly Error SectionNotFound = Error.NotFound("SECTION_NOT_FOUND", "The course section was not found.");
        public static readonly Error SectionCodeExists = Error.Conflict("SECTION_CODE_EXISTS", "A section with this code already exists in the offering.");
        public static readonly Error SectionInvalidState = Error.BusinessRule("SECTION_INVALID_STATE", "The section is not in a state that allows this action.");
        public static readonly Error SectionHasEnrollments = Error.BusinessRule("SECTION_HAS_ENROLLMENTS", "Students are enrolled in the section, so it cannot be cancelled.");
        public static readonly Error CapacityBelowEnrolled = Error.BusinessRule("SECTION_CAPACITY_BELOW_ENROLLED", "The capacity cannot be lower than the number of enrolled students.");

        // Instructor assignments.
        public static readonly Error AssignmentInvalid = Error.Validation("ASSIGNMENT_INVALID", "The section, the instructor and a valid role are required.");
        public static readonly Error AssignmentNotFound = Error.NotFound("ASSIGNMENT_NOT_FOUND", "The instructor assignment was not found.");
        public static readonly Error AssignmentAlreadyEnded = Error.BusinessRule("ASSIGNMENT_ALREADY_ENDED", "The assignment has already ended.");
        public static readonly Error AssignmentOverlap = Error.Conflict("ASSIGNMENT_OVERLAP", "The instructor already has an active assignment in this section.");
        public static readonly Error PrimaryExists = Error.Conflict("ASSIGNMENT_PRIMARY_EXISTS", "The section already has an active primary instructor. End that assignment first.");
        public static readonly Error InstructorNotFound = Error.NotFound("ASSIGNMENT_INSTRUCTOR_NOT_FOUND", "The instructor was not found.");
        public static readonly Error InstructorInactive = Error.BusinessRule("ASSIGNMENT_INSTRUCTOR_INACTIVE", "The instructor is inactive.");
        public static readonly Error SectionNotAssignable = Error.BusinessRule("ASSIGNMENT_SECTION_NOT_ASSIGNABLE", "Instructors cannot be assigned to a cancelled section.");
    }
}
