using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Curriculum
{
    // Error codes of the Curriculum module (study plans, versions, plan courses, graduation requirements).
    public static class CurriculumErrors
    {
        // Study plan fields.
        public static readonly Error InvalidCode = Error.Validation("STUDY_PLAN_CODE_INVALID", "The study plan code is required, 2 to 20 characters, using only letters, digits, '-' and '_'.");
        public static readonly Error InvalidName = Error.Validation("STUDY_PLAN_NAME_INVALID", "The study plan name is required and must be 2 to 200 characters.");
        public static readonly Error InvalidDescription = Error.Validation("STUDY_PLAN_DESCRIPTION_INVALID", "The description must be at most 1000 characters.");
        public static readonly Error InvalidParent = Error.Validation("STUDY_PLAN_PARENT_INVALID", "The program id and the study plan id are required.");
        public static readonly Error PlanNotFound = Error.NotFound("STUDY_PLAN_NOT_FOUND", "The study plan was not found.");
        public static readonly Error PlanCodeExists = Error.Conflict("STUDY_PLAN_CODE_EXISTS", "A study plan with this code already exists in the program.");
        public static readonly Error PlanInactive = Error.BusinessRule("STUDY_PLAN_INACTIVE", "The study plan is inactive, so it cannot get new versions.");
        public static readonly Error PlanAlreadyActive = Error.BusinessRule("STUDY_PLAN_ALREADY_ACTIVE", "The study plan is already active.");
        public static readonly Error PlanAlreadyInactive = Error.BusinessRule("STUDY_PLAN_ALREADY_INACTIVE", "The study plan is already inactive.");
        public static readonly Error PlanHasActiveVersion = Error.BusinessRule("STUDY_PLAN_HAS_ACTIVE_VERSION", "Retire the active version before deactivating the study plan.");
        public static readonly Error ProgramNotFound = Error.NotFound("STUDY_PLAN_PROGRAM_NOT_FOUND", "The academic program was not found.");
        public static readonly Error ProgramInactive = Error.BusinessRule("STUDY_PLAN_PROGRAM_INACTIVE", "Study plans cannot be added to an inactive program.");

        // Versions.
        public static readonly Error VersionNotFound = Error.NotFound("STUDY_PLAN_VERSION_NOT_FOUND", "The study plan version was not found.");
        public static readonly Error VersionNumberInvalid = Error.Validation("STUDY_PLAN_VERSION_NUMBER_INVALID", "The version number must be 1 or more.");
        public static readonly Error VersionNotDraft = Error.BusinessRule("STUDY_PLAN_VERSION_NOT_DRAFT", "Only a draft version can be changed. Create a new draft from this version instead.");
        public static readonly Error VersionNotActive = Error.BusinessRule("STUDY_PLAN_VERSION_NOT_ACTIVE", "The study plan version is not active.");
        public static readonly Error VersionInvalidState = Error.BusinessRule("STUDY_PLAN_VERSION_INVALID_STATE", "The version is not in a state that allows this action.");
        public static readonly Error DraftExists = Error.Conflict("STUDY_PLAN_DRAFT_EXISTS", "The study plan already has a draft version.");
        public static readonly Error VersionEmpty = Error.BusinessRule("STUDY_PLAN_VERSION_EMPTY", "A version needs at least one course before it can be activated.");
        public static readonly Error EffectiveDateInvalid = Error.Validation("STUDY_PLAN_EFFECTIVE_DATE_INVALID", "The effective date is required and must be after the effective date of the version it replaces.");
        public static readonly Error EffectiveToInvalid = Error.Validation("STUDY_PLAN_EFFECTIVE_TO_INVALID", "The end date must not be before the effective date.");
        public static readonly Error ProgramMismatch = Error.BusinessRule("STUDY_PLAN_VERSION_PROGRAM_MISMATCH", "The study plan version does not belong to the program.");

        // Plan courses.
        public static readonly Error CourseVersionInvalid = Error.Validation("STUDY_PLAN_COURSE_VERSION_INVALID", "The course version id is required.");
        public static readonly Error CourseVersionNotFound = Error.NotFound("STUDY_PLAN_COURSE_VERSION_NOT_FOUND", "The course version was not found or cannot be used.");
        public static readonly Error CourseNotFound = Error.NotFound("STUDY_PLAN_COURSE_NOT_FOUND", "The course is not part of this version.");
        public static readonly Error CourseDuplicate = Error.Conflict("STUDY_PLAN_COURSE_DUPLICATE", "The course is already part of this version.");
        public static readonly Error TooManyCourses = Error.BusinessRule("STUDY_PLAN_TOO_MANY_COURSES", "A version can have at most 200 courses.");
        public static readonly Error RequirementTypeInvalid = Error.Validation("STUDY_PLAN_REQUIREMENT_TYPE_INVALID", "The requirement type is not valid.");
        public static readonly Error RequiredCreditsInvalid = Error.Validation("STUDY_PLAN_REQUIRED_CREDITS_INVALID", "The credits must be between 1 and 30.");
        public static readonly Error LevelNotFound = Error.NotFound("STUDY_PLAN_LEVEL_NOT_FOUND", "The academic level was not found.");
        public static readonly Error LevelInvalid = Error.BusinessRule("STUDY_PLAN_LEVEL_INVALID", "The academic level must be active and belong to the same program.");
        public static readonly Error TermNotFound = Error.NotFound("STUDY_PLAN_TERM_NOT_FOUND", "The academic term was not found.");
        public static readonly Error TermInactive = Error.BusinessRule("STUDY_PLAN_TERM_INACTIVE", "The academic term is inactive.");

        // Course requirements (graduation rules).
        public static readonly Error RequirementNotFound = Error.NotFound("COURSE_REQUIREMENT_NOT_FOUND", "The course requirement was not found.");
    }
}
