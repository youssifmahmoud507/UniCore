namespace UniCore.Domain.Modules.Grading
{
    public static class GradeChangeAuditActions
    {
        public const string Requested = "GRADE_CHANGE_REQUESTED";
        public const string Applied = "GRADE_CHANGE_APPLIED";
        public const string Rejected = "GRADE_CHANGE_REJECTED";
        public const string Cancelled = "GRADE_CHANGE_CANCELLED";
        public const string Failed = "GRADE_CHANGE_FAILED";
    }
}
