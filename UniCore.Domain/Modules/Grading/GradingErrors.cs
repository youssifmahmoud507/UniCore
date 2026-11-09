using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Grading
{
    public static class GradingErrors
    {
        public static readonly Error Invalid = Error.Validation("GRADE_INVALID", "A section, a student and at least one score are required.");
        public static readonly Error NotFound = Error.NotFound("GRADE_NOT_FOUND", "The grade was not found.");
        public static readonly Error InvalidState = Error.BusinessRule("GRADE_INVALID_STATE", "The grade is not in a state that allows this operation.");
        public static readonly Error Locked = Error.BusinessRule("GRADE_LOCKED", "A published or locked grade changes only through an approved grade change request.");
        public static readonly Error SectionNotFound = Error.NotFound("GRADE_SECTION_NOT_FOUND", "The course section was not found.");
        public static readonly Error SemesterClosed = Error.BusinessRule("GRADE_SEMESTER_CLOSED", "The semester is closed.");
        public static readonly Error WindowClosed = Error.BusinessRule("GRADE_WINDOW_CLOSED", "Grades can be entered from the first day of the semester until its grade submission deadline.");
        public static readonly Error PublicationDateNotReached = Error.BusinessRule("GRADE_PUBLICATION_DATE_NOT_REACHED", "The grade publication date of the semester has not been reached.");
        public static readonly Error StudentNotOnRoster = Error.BusinessRule("GRADE_STUDENT_NOT_ON_ROSTER", "The student is not registered in this section.");
        public static readonly Error StudentNotFound = Error.NotFound("GRADE_STUDENT_NOT_FOUND", "The student was not found.");
        public static readonly Error AssessmentNotReady = Error.BusinessRule("GRADE_ASSESSMENT_NOT_READY", "The section has no published grading scheme (assessment).");
        public static readonly Error ComponentNotInScheme = Error.Validation("GRADE_COMPONENT_NOT_IN_SCHEME", "A score names a component that is not in the grading scheme of the section.");
        public static readonly Error ScoreInvalid = Error.Validation("GRADE_SCORE_INVALID", "A score must be between 0 and the maximum score of its component (two decimals at most).");
        public static readonly Error ScoresIncomplete = Error.BusinessRule("GRADE_SCORES_INCOMPLETE", "Every component of the scheme needs a score before the grade can be submitted.");
        public static readonly Error SectionIncomplete = Error.BusinessRule("GRADE_SECTION_INCOMPLETE", "Every registered student of the section needs a grade before the section can be submitted.");
        public static readonly Error NothingToProcess = Error.BusinessRule("GRADE_NOTHING_TO_PROCESS", "The section has no grades in the state this step works on.");
        public static readonly Error SeparationOfDuties = Error.BusinessRule("GRADE_SEPARATION_OF_DUTIES", "The person who entered or reviewed a grade cannot review or approve it.");
        public static readonly Error ReasonRequired = Error.Validation("GRADE_REASON_REQUIRED", "A reason (up to 500 characters) is required.");
        public static readonly Error CorrectionInvalid = Error.Validation("GRADE_CORRECTION_INVALID", "A correction needs an existing component of the grade, a valid score and a reason.");
        public static readonly Error ScaleNotFound = Error.NotFound("GRADE_SCALE_NOT_FOUND", "There is no default grade scale.");
        public static readonly Error ScaleInvalid = Error.Validation("GRADE_SCALE_INVALID", "A scale needs a name, at least two bands with unique letters and minimum percentages, and a band starting at 0.");
        public static readonly Error PercentageInvalid = Error.Validation("GRADE_PERCENTAGE_INVALID", "A percentage must be between 0 and 100.");
    }
}
