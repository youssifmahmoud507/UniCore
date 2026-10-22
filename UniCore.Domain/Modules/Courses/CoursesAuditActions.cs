namespace UniCore.Domain.Modules.Courses
{
    // Audit action names of the Courses module (kept apart from AuditActions so the two developers
    // do not edit the same file).
    public static class CoursesAuditActions
    {
        public const string CourseCreated = "COURSE_CREATED";
        public const string CourseUpdated = "COURSE_UPDATED";
        public const string CourseActivated = "COURSE_ACTIVATED";
        public const string CourseDeactivated = "COURSE_DEACTIVATED";

        public const string VersionCreated = "COURSE_VERSION_CREATED";
        public const string VersionUpdated = "COURSE_VERSION_UPDATED";
        public const string VersionActivated = "COURSE_VERSION_ACTIVATED";
        public const string VersionRetired = "COURSE_VERSION_RETIRED";
    }
}
