using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Attendance
{
    public static class AttendanceErrors
    {
        // Policy.
        public static readonly Error PolicyInvalid = Error.Validation("ATTENDANCE_POLICY_INVALID", "A policy belongs to exactly one program or one course, with 0 <= minimum <= warning <= 100 and a late threshold of 0 to 180 minutes.");
        public static readonly Error PolicyNotFound = Error.NotFound("ATTENDANCE_POLICY_NOT_FOUND", "The attendance policy was not found.");
        public static readonly Error PolicyExists = Error.Conflict("ATTENDANCE_POLICY_EXISTS", "An active policy already exists for this program or course.");
        public static readonly Error PolicyTargetNotFound = Error.NotFound("ATTENDANCE_POLICY_TARGET_NOT_FOUND", "The program or course of the policy was not found.");

        // Session.
        public static readonly Error SessionInvalid = Error.Validation("ATTENDANCE_SESSION_INVALID", "The section, the date and the start time are required.");
        public static readonly Error SessionNotFound = Error.NotFound("ATTENDANCE_SESSION_NOT_FOUND", "The attendance session was not found.");
        public static readonly Error SessionExists = Error.Conflict("ATTENDANCE_SESSION_EXISTS", "The section already has a session at this date and time.");
        public static readonly Error SessionClosed = Error.BusinessRule("ATTENDANCE_SESSION_CLOSED", "The session is closed. Attendance can only be recorded in an open session.");
        public static readonly Error SessionNotOpen = Error.BusinessRule("ATTENDANCE_SESSION_NOT_OPEN", "The session is not open.");
        public static readonly Error SessionDateOutsideSemester = Error.BusinessRule("ATTENDANCE_SESSION_DATE_OUTSIDE_SEMESTER", "The session date is outside the semester of the section.");
        public static readonly Error SectionNotFound = Error.NotFound("ATTENDANCE_SECTION_NOT_FOUND", "The course section was not found.");
        public static readonly Error SemesterClosed = Error.BusinessRule("ATTENDANCE_SEMESTER_CLOSED", "The semester is closed, so attendance cannot change.");

        // Records.
        public static readonly Error RecordInvalid = Error.Validation("ATTENDANCE_RECORD_INVALID", "The student and a valid status are required. Late needs 1 to 240 minutes, and Excused cannot be marked directly.");
        public static readonly Error RecordNotFound = Error.NotFound("ATTENDANCE_RECORD_NOT_FOUND", "The attendance record was not found.");
        public static readonly Error RecordExcused = Error.BusinessRule("ATTENDANCE_RECORD_EXCUSED", "The absence was excused. It cannot be changed.");
        public static readonly Error StudentNotOnRoster = Error.BusinessRule("ATTENDANCE_STUDENT_NOT_ENROLLED", "The student is not enrolled in this section.");
        public static readonly Error MarksRequired = Error.Validation("ATTENDANCE_MARKS_REQUIRED", "At least one student is required, and a student can appear once.");

        // Excuses.
        public static readonly Error ExcuseInvalid = Error.Validation("ATTENDANCE_EXCUSE_INVALID", "The reason is required (5 to 1000 characters).");
        public static readonly Error ExcuseNotFound = Error.NotFound("ATTENDANCE_EXCUSE_NOT_FOUND", "The excuse was not found.");
        public static readonly Error ExcuseNotAbsence = Error.BusinessRule("ATTENDANCE_EXCUSE_NOT_ABSENCE", "An excuse can only be submitted for an absence.");
        public static readonly Error ExcuseExists = Error.Conflict("ATTENDANCE_EXCUSE_EXISTS", "This absence already has a pending or accepted excuse.");
        public static readonly Error ExcuseNotPending = Error.BusinessRule("ATTENDANCE_EXCUSE_NOT_PENDING", "The excuse was already reviewed.");
        public static readonly Error ExcuseRejectNeedsNote = Error.Validation("ATTENDANCE_EXCUSE_NOTE_REQUIRED", "A rejection needs a note (up to 1000 characters).");
        public static readonly Error ExcuseOwnOnly = Error.Forbidden("ATTENDANCE_EXCUSE_NOT_YOURS", "An excuse can only be submitted for the student's own absence.");

        // Reading.
        public static readonly Error FilterRequired = Error.Validation("ATTENDANCE_FILTER_REQUIRED", "Give a section or a student to list excuses.");
        public static readonly Error StudentNotFound = Error.NotFound("ATTENDANCE_STUDENT_NOT_FOUND", "The student was not found.");
    }
}
