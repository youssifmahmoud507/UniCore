namespace UniCore.Domain.Modules.Examination
{
    public static class ExaminationAuditActions
    {
        public const string SessionCreated = "EXAM_SESSION_CREATED";
        public const string SessionClosed = "EXAM_SESSION_CLOSED";
        public const string ExamCreated = "EXAM_CREATED";
        public const string ExamUpdated = "EXAM_UPDATED";
        public const string ScheduleSet = "EXAM_SCHEDULE_SET";
        public const string RoomAdded = "EXAM_ROOM_ADDED";
        public const string RoomRemoved = "EXAM_ROOM_REMOVED";
        public const string InvigilatorAdded = "EXAM_INVIGILATOR_ADDED";
        public const string InvigilatorRemoved = "EXAM_INVIGILATOR_REMOVED";
        public const string ExamPublished = "EXAM_PUBLISHED";
        public const string ExamCancelled = "EXAM_CANCELLED";
    }
}
