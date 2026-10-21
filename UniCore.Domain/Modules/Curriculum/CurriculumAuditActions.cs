namespace UniCore.Domain.Modules.Curriculum
{
    // Audit action names of the Curriculum module (kept apart from AuditActions so the two developers
    // do not edit the same file).
    public static class CurriculumAuditActions
    {
        public const string StudyPlanCreated = "STUDY_PLAN_CREATED";
        public const string StudyPlanUpdated = "STUDY_PLAN_UPDATED";
        public const string StudyPlanActivated = "STUDY_PLAN_ACTIVATED";
        public const string StudyPlanDeactivated = "STUDY_PLAN_DEACTIVATED";

        public const string VersionCreated = "STUDY_PLAN_VERSION_CREATED";
        public const string VersionUpdated = "STUDY_PLAN_VERSION_UPDATED";
        public const string VersionActivated = "STUDY_PLAN_VERSION_ACTIVATED";
        public const string VersionRetired = "STUDY_PLAN_VERSION_RETIRED";

        public const string CourseAdded = "STUDY_PLAN_COURSE_ADDED";
        public const string CourseUpdated = "STUDY_PLAN_COURSE_UPDATED";
        public const string CourseRemoved = "STUDY_PLAN_COURSE_REMOVED";

        public const string RequirementSet = "COURSE_REQUIREMENT_SET";
        public const string RequirementRemoved = "COURSE_REQUIREMENT_REMOVED";
    }
}
