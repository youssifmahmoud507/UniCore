using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Assessments
{
    public static class AssessmentErrors
    {
        public static readonly Error Invalid = Error.Validation("ASSESSMENT_INVALID", "A section is required.");
        public static readonly Error NotFound = Error.NotFound("ASSESSMENT_NOT_FOUND", "The assessment was not found.");
        public static readonly Error Exists = Error.Conflict("ASSESSMENT_EXISTS", "The section already has a live assessment (draft, published or closed).");
        public static readonly Error InvalidState = Error.BusinessRule("ASSESSMENT_INVALID_STATE", "The assessment is not in a state that allows this operation.");
        public static readonly Error SectionNotFound = Error.NotFound("ASSESSMENT_SECTION_NOT_FOUND", "The course section was not found.");
        public static readonly Error SemesterNotFound = Error.NotFound("ASSESSMENT_SEMESTER_NOT_FOUND", "The semester of the section was not found.");
        public static readonly Error SemesterClosed = Error.BusinessRule("ASSESSMENT_SEMESTER_CLOSED", "The semester is closed.");

        public static readonly Error ScoreInvalid = Error.Validation("ASSESSMENT_SCORE_INVALID", "A score must be between 0 and the maximum score of a component of the scheme.");

        public static readonly Error ComponentInvalid = Error.Validation("ASSESSMENT_COMPONENT_INVALID", "A name (up to 100 characters), a valid type, a weight above 0 up to 100 (two decimals at most), a maximum score above 0 up to 1000 and a valid due date are required.");
        public static readonly Error ComponentNotFound = Error.NotFound("ASSESSMENT_COMPONENT_NOT_FOUND", "The component was not found.");
        public static readonly Error ComponentNameExists = Error.Conflict("ASSESSMENT_COMPONENT_NAME_EXISTS", "The assessment already has a component with this name.");
        public static readonly Error ComponentTypeExists = Error.Conflict("ASSESSMENT_COMPONENT_TYPE_EXISTS", "The assessment already has a midterm or final component of this type.");
        public static readonly Error TooManyComponents = Error.BusinessRule("ASSESSMENT_TOO_MANY_COMPONENTS", "An assessment has at most 20 components.");
        public static readonly Error WeightExceeded = Error.BusinessRule("ASSESSMENT_WEIGHT_EXCEEDED", "The weights of the components cannot total more than 100.");
        public static readonly Error WeightNotHundred = Error.BusinessRule("ASSESSMENT_WEIGHT_NOT_100", "The weights of the components must total exactly 100 to publish.");
        public static readonly Error NoComponents = Error.BusinessRule("ASSESSMENT_NO_COMPONENTS", "The assessment needs at least one component.");
        public static readonly Error DueDateMissing = Error.BusinessRule("ASSESSMENT_DUE_DATE_MISSING", "Every component needs a due date to publish.");
        public static readonly Error DueDateOutsideSemester = Error.BusinessRule("ASSESSMENT_DUE_DATE_OUTSIDE_SEMESTER", "The due date must be inside the dates of the semester.");
        public static readonly Error DueDateOutsideWindow = Error.BusinessRule("ASSESSMENT_DUE_DATE_OUTSIDE_WINDOW", "A midterm or final component must be due inside the exam window of the semester.");
    }
}
