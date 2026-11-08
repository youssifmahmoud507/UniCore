namespace UniCore.Domain.Modules.Attendance
{
    public static class AttendanceAuditActions
    {
        public const string PolicyCreated = "ATTENDANCE_POLICY_CREATED";
        public const string PolicyUpdated = "ATTENDANCE_POLICY_UPDATED";
        public const string PolicyDeactivated = "ATTENDANCE_POLICY_DEACTIVATED";
        public const string SessionOpened = "ATTENDANCE_SESSION_OPENED";
        public const string SessionClosed = "ATTENDANCE_SESSION_CLOSED";
        public const string AttendanceMarked = "ATTENDANCE_MARKED";
        public const string ExcuseSubmitted = "ATTENDANCE_EXCUSE_SUBMITTED";
        public const string ExcuseAccepted = "ATTENDANCE_EXCUSE_ACCEPTED";
        public const string ExcuseRejected = "ATTENDANCE_EXCUSE_REJECTED";
        public const string WarningIssued = "ATTENDANCE_WARNING_ISSUED";
    }
}
