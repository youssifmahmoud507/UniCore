namespace UniCore.Domain.Modules.Calendar
{
    // Audit action names written by the calendar handlers.
    public static class CalendarAuditActions
    {
        public const string YearCreated = "ACADEMIC_YEAR_CREATED";
        public const string YearUpdated = "ACADEMIC_YEAR_UPDATED";
        public const string YearActivated = "ACADEMIC_YEAR_ACTIVATED";
        public const string YearMadeCurrent = "ACADEMIC_YEAR_MADE_CURRENT";
        public const string YearClosed = "ACADEMIC_YEAR_CLOSED";

        public const string TermCreated = "ACADEMIC_TERM_CREATED";
        public const string TermUpdated = "ACADEMIC_TERM_UPDATED";
        public const string TermActivated = "ACADEMIC_TERM_ACTIVATED";
        public const string TermDeactivated = "ACADEMIC_TERM_DEACTIVATED";

        public const string SemesterCreated = "SEMESTER_CREATED";
        public const string SemesterRescheduled = "SEMESTER_RESCHEDULED";
        public const string SemesterStatusChanged = "SEMESTER_STATUS_CHANGED";

        public const string LevelCreated = "ACADEMIC_LEVEL_CREATED";
        public const string LevelUpdated = "ACADEMIC_LEVEL_UPDATED";
        public const string LevelActivated = "ACADEMIC_LEVEL_ACTIVATED";
        public const string LevelDeactivated = "ACADEMIC_LEVEL_DEACTIVATED";
    }
}
