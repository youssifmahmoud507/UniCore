namespace UniCore.Domain.Modules.University
{
    // Audit action names of the University module (kept apart from AuditActions so the two developers
    // do not edit the same file).
    public static class UniversityAuditActions
    {
        public const string UniversityCreated = "UNIVERSITY_CREATED";
        public const string UniversityUpdated = "UNIVERSITY_UPDATED";
        public const string UniversityActivated = "UNIVERSITY_ACTIVATED";
        public const string UniversityDeactivated = "UNIVERSITY_DEACTIVATED";

        public const string CampusCreated = "CAMPUS_CREATED";
        public const string CampusUpdated = "CAMPUS_UPDATED";
        public const string CampusActivated = "CAMPUS_ACTIVATED";
        public const string CampusDeactivated = "CAMPUS_DEACTIVATED";

        public const string FacultyCreated = "FACULTY_CREATED";
        public const string FacultyUpdated = "FACULTY_UPDATED";
        public const string FacultyActivated = "FACULTY_ACTIVATED";
        public const string FacultyDeactivated = "FACULTY_DEACTIVATED";

        public const string DepartmentCreated = "DEPARTMENT_CREATED";
        public const string DepartmentUpdated = "DEPARTMENT_UPDATED";
        public const string DepartmentActivated = "DEPARTMENT_ACTIVATED";
        public const string DepartmentDeactivated = "DEPARTMENT_DEACTIVATED";

        public const string ProgramCreated = "PROGRAM_CREATED";
        public const string ProgramUpdated = "PROGRAM_UPDATED";
        public const string ProgramActivated = "PROGRAM_ACTIVATED";
        public const string ProgramDeactivated = "PROGRAM_DEACTIVATED";
    }
}
