using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Courses
{
    // Error codes of the Courses module (courses and their versions).
    public static class CoursesErrors
    {
        // Course fields.
        public static readonly Error InvalidCode = Error.Validation("COURSE_CODE_INVALID", "The course code is required, 2 to 20 characters, using only letters, digits, '-' and '_'.");
        public static readonly Error InvalidTitle = Error.Validation("COURSE_TITLE_INVALID", "The course title is required and must be 2 to 200 characters.");
        public static readonly Error InvalidDescription = Error.Validation("COURSE_DESCRIPTION_INVALID", "The description must be at most 1000 characters.");
        public static readonly Error InvalidCourseType = Error.Validation("COURSE_TYPE_INVALID", "The course type is required and must be a known value.");
        public static readonly Error InvalidParent = Error.Validation("COURSE_PARENT_INVALID", "The department id and the course id are required.");
        public static readonly Error CourseNotFound = Error.NotFound("COURSE_NOT_FOUND", "The course was not found.");
        public static readonly Error CourseCodeExists = Error.Conflict("COURSE_CODE_EXISTS", "A course with this code already exists in the department.");
        public static readonly Error CourseInactive = Error.BusinessRule("COURSE_INACTIVE", "The course is inactive, so it cannot get new versions.");
        public static readonly Error CourseAlreadyActive = Error.BusinessRule("COURSE_ALREADY_ACTIVE", "The course is already active.");
        public static readonly Error CourseAlreadyInactive = Error.BusinessRule("COURSE_ALREADY_INACTIVE", "The course is already inactive.");
        public static readonly Error CourseHasActiveVersion = Error.BusinessRule("COURSE_HAS_ACTIVE_VERSION", "Retire the active version before deactivating the course.");
        public static readonly Error DepartmentNotFound = Error.NotFound("COURSE_DEPARTMENT_NOT_FOUND", "The department was not found.");
        public static readonly Error DepartmentInactive = Error.BusinessRule("COURSE_DEPARTMENT_INACTIVE", "Courses cannot be added to an inactive department.");

        // Versions.
        public static readonly Error VersionNotFound = Error.NotFound("COURSE_VERSION_NOT_FOUND", "The course version was not found.");
        public static readonly Error VersionNumberInvalid = Error.Validation("COURSE_VERSION_NUMBER_INVALID", "The version number must be 1 or more.");
        public static readonly Error VersionNotDraft = Error.BusinessRule("COURSE_VERSION_NOT_DRAFT", "Only a draft version can be changed. Create a new draft from this version instead.");
        public static readonly Error VersionInvalidState = Error.BusinessRule("COURSE_VERSION_INVALID_STATE", "The version is not in a state that allows this action.");
        public static readonly Error DraftExists = Error.Conflict("COURSE_DRAFT_EXISTS", "The course already has a draft version.");
        public static readonly Error CreditsInvalid = Error.Validation("COURSE_CREDITS_INVALID", "The credits are required and must be between 1 and 30.");
        public static readonly Error HoursInvalid = Error.Validation("COURSE_HOURS_INVALID", "Lecture and practical hours must be between 0 and 60, and at least one of them must be more than 0.");
        public static readonly Error EffectiveDateInvalid = Error.Validation("COURSE_VERSION_EFFECTIVE_DATE_INVALID", "The effective date is required and must be after the effective date of the version it replaces.");
        public static readonly Error EffectiveToInvalid = Error.Validation("COURSE_VERSION_EFFECTIVE_TO_INVALID", "The end date is required and must not be before the effective date.");
        public static readonly Error VersionOverlap = Error.BusinessRule("COURSE_VERSION_OVERLAP", "The version would overlap in time with an earlier version of the same course.");
    }
}
