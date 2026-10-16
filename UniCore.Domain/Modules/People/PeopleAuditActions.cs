namespace UniCore.Domain.Modules.People
{
    // Audit action names written by the people handlers. Values never contain the national id.
    public static class PeopleAuditActions
    {
        public const string PersonCreated = "PERSON_CREATED";
        public const string PersonUpdated = "PERSON_UPDATED";
        public const string PersonUserLinked = "PERSON_USER_LINKED";

        public const string EmployeeCreated = "EMPLOYEE_CREATED";
        public const string EmployeeUpdated = "EMPLOYEE_UPDATED";
        public const string EmployeeTerminated = "EMPLOYEE_TERMINATED";

        public const string InstructorCreated = "INSTRUCTOR_CREATED";
        public const string InstructorUpdated = "INSTRUCTOR_UPDATED";
        public const string InstructorActivated = "INSTRUCTOR_ACTIVATED";
        public const string InstructorDeactivated = "INSTRUCTOR_DEACTIVATED";

        public const string TeachingAssistantCreated = "TEACHING_ASSISTANT_CREATED";
        public const string TeachingAssistantActivated = "TEACHING_ASSISTANT_ACTIVATED";
        public const string TeachingAssistantDeactivated = "TEACHING_ASSISTANT_DEACTIVATED";
    }
}
