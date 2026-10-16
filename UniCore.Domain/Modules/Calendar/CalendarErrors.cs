using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Calendar
{
    // Error codes for the academic calendar (year, term, semester) and academic levels.
    public static class CalendarErrors
    {
        public static readonly Error InvalidName = Error.Validation("INVALID_NAME", "The name is required and must be 2 to 200 characters.");
        public static readonly Error InvalidCode = Error.Validation("INVALID_CODE", "The code is required, 2 to 20 characters, using only letters, digits, '-' and '_'.");
        public static readonly Error InvalidParent = Error.Validation("INVALID_PARENT", "The parent id is required.");
        public static readonly Error InvalidTermOrder = Error.Validation("INVALID_TERM_ORDER", "The term order must be between 1 and 10.");
        public static readonly Error InvalidLevelOrder = Error.Validation("INVALID_LEVEL_ORDER", "The level order must be between 1 and 20.");
        public static readonly Error InvalidLevelCredits = Error.Validation("INVALID_LEVEL_CREDITS", "The credits required for a level must be between 0 and 400.");
        public static readonly Error InvalidSemesterStatus = Error.Validation("INVALID_SEMESTER_STATUS", "The semester status is not valid.");

        public static readonly Error AcademicYearInvalidDates = Error.Validation("ACADEMIC_YEAR_INVALID_DATES", "The start date must be before the end date.");
        public static readonly Error AcademicYearCurrentExists = Error.BusinessRule("ACADEMIC_YEAR_CURRENT_EXISTS", "Another academic year is already the current one.");
        public static readonly Error AcademicYearOverlap = Error.BusinessRule("ACADEMIC_YEAR_OVERLAP", "The academic year overlaps another academic year.");
        public static readonly Error AcademicYearInvalidState = Error.BusinessRule("ACADEMIC_YEAR_INVALID_STATE", "The academic year cannot move to that state from its current state.");
        public static readonly Error AcademicYearClosed = Error.BusinessRule("ACADEMIC_YEAR_CLOSED", "The academic year is closed and cannot be changed.");
        public static readonly Error AcademicYearHasOpenSemesters = Error.BusinessRule("ACADEMIC_YEAR_HAS_OPEN_SEMESTERS", "The academic year still has semesters that are not closed.");

        public static readonly Error SemesterInvalidDates = Error.Validation("SEMESTER_INVALID_DATES", "The semester dates or its date windows are not in a valid order.");
        public static readonly Error SemesterOutsideYear = Error.BusinessRule("SEMESTER_OUTSIDE_YEAR", "The semester must lie inside its academic year.");
        public static readonly Error SemesterOverlap = Error.BusinessRule("SEMESTER_OVERLAP", "The semester overlaps another semester of the same academic year.");
        public static readonly Error SemesterDuplicateTerm = Error.Conflict("SEMESTER_DUPLICATE_TERM", "The academic year already has a semester for this term.");
        public static readonly Error SemesterWindowsIncomplete = Error.BusinessRule("SEMESTER_WINDOWS_INCOMPLETE", "The registration window and the add/drop window must be set first.");
        public static readonly Error SemesterInvalidState = Error.BusinessRule("SEMESTER_INVALID_STATE", "A semester can only move forward to a later status.");
        public static readonly Error SemesterClosed = Error.BusinessRule("SEMESTER_CLOSED", "The semester is closed.");
        public static readonly Error NoCurrentSemester = Error.NotFound("NO_CURRENT_SEMESTER", "There is no current semester.");

        public static readonly Error AcademicYearNotFound = Error.NotFound("ACADEMIC_YEAR_NOT_FOUND", "The academic year was not found.");
        public static readonly Error AcademicTermNotFound = Error.NotFound("ACADEMIC_TERM_NOT_FOUND", "The academic term was not found.");
        public static readonly Error SemesterNotFound = Error.NotFound("SEMESTER_NOT_FOUND", "The semester was not found.");
        public static readonly Error LevelNotFound = Error.NotFound("ACADEMIC_LEVEL_NOT_FOUND", "The academic level was not found.");
        public static readonly Error LevelProgramMismatch = Error.Validation("LEVEL_PROGRAM_MISMATCH", "The academic level does not belong to the program.");
        public static readonly Error LevelInactive = Error.BusinessRule("ACADEMIC_LEVEL_INACTIVE", "The academic level is inactive.");
        public static readonly Error TermInactive = Error.BusinessRule("ACADEMIC_TERM_INACTIVE", "The academic term is inactive.");
        public static readonly Error AdmissionYearMismatch = Error.Validation("ADMISSION_YEAR_MISMATCH", "The admission year does not match the academic year.");

        public static readonly Error AlreadyActive = Error.BusinessRule("ALREADY_ACTIVE", "The record is already active.");
        public static readonly Error AlreadyInactive = Error.BusinessRule("ALREADY_INACTIVE", "The record is already inactive.");
    }
}
