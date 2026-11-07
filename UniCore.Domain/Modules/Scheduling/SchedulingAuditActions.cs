namespace UniCore.Domain.Modules.Scheduling
{
    public static class SchedulingAuditActions
    {
        public const string ScheduleCreated = "SCHEDULE_CREATED";
        public const string ScheduleUpdated = "SCHEDULE_UPDATED";
        public const string SchedulePublished = "SCHEDULE_PUBLISHED";
        public const string ScheduleArchived = "SCHEDULE_ARCHIVED";
        public const string SlotAdded = "SCHEDULE_SLOT_ADDED";
        public const string SlotRemoved = "SCHEDULE_SLOT_REMOVED";
        public const string SectionScheduled = "SECTION_SCHEDULED";
        public const string SectionUnscheduled = "SECTION_UNSCHEDULED";
        public const string InstructorAvailabilityAdded = "INSTRUCTOR_AVAILABILITY_ADDED";
        public const string InstructorAvailabilityRemoved = "INSTRUCTOR_AVAILABILITY_REMOVED";
        public const string RoomAvailabilityAdded = "ROOM_AVAILABILITY_ADDED";
        public const string RoomAvailabilityRemoved = "ROOM_AVAILABILITY_REMOVED";
    }
}
