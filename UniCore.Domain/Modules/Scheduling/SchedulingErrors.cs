using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Scheduling
{
    public static class SchedulingErrors
    {
        // Schedule.
        public static readonly Error ScheduleInvalid = Error.Validation("SCHEDULE_INVALID", "The semester is required and the name must be 2 to 200 characters.");
        public static readonly Error ScheduleNotFound = Error.NotFound("SCHEDULE_NOT_FOUND", "The schedule was not found.");
        public static readonly Error ScheduleNameExists = Error.Conflict("SCHEDULE_NAME_EXISTS", "This semester already has a schedule with this name.");
        public static readonly Error ScheduleInvalidState = Error.BusinessRule("SCHEDULE_INVALID_STATE", "The schedule is not in a state that allows this action.");
        public static readonly Error ScheduleNeedsSlot = Error.BusinessRule("SCHEDULE_NEEDS_SLOT", "A schedule needs at least one time slot before it can be published.");
        public static readonly Error SemesterNotFound = Error.NotFound("SCHEDULE_SEMESTER_NOT_FOUND", "The semester was not found.");
        public static readonly Error SemesterClosed = Error.BusinessRule("SCHEDULE_SEMESTER_CLOSED", "The semester is closed, so its schedules cannot change.");

        // Slots and time windows.
        public static readonly Error TimeInvalid = Error.Validation("SCHEDULE_TIME_INVALID", "Start and end must be whole minutes, the end after the start, and at least 30 minutes apart (a slot is at most 8 hours).");
        public static readonly Error DayInvalid = Error.Validation("SCHEDULE_DAY_INVALID", "The day of the week is not valid.");
        public static readonly Error SlotNotFound = Error.NotFound("SCHEDULE_SLOT_NOT_FOUND", "The time slot was not found in this schedule.");
        public static readonly Error SlotExists = Error.Conflict("SCHEDULE_SLOT_EXISTS", "The schedule already has this time slot.");
        public static readonly Error SlotInUse = Error.BusinessRule("SCHEDULE_SLOT_IN_USE", "Sections are scheduled in this slot. Unschedule them first.");

        // Placing a section.
        public static readonly Error AssignmentInvalid = Error.Validation("SECTION_SCHEDULE_INVALID", "The section, the slot and the room are required.");
        public static readonly Error AssignmentNotFound = Error.NotFound("SECTION_SCHEDULE_NOT_FOUND", "The section is not scheduled there.");
        public static readonly Error SectionNotFound = Error.NotFound("SCHEDULE_SECTION_NOT_FOUND", "The course section was not found.");
        public static readonly Error SectionNotSchedulable = Error.BusinessRule("SCHEDULE_SECTION_NOT_SCHEDULABLE", "A cancelled section cannot be scheduled.");
        public static readonly Error SectionSemesterMismatch = Error.BusinessRule("SCHEDULE_SECTION_SEMESTER_MISMATCH", "The section is offered in another semester than the schedule.");
        public static readonly Error SectionAlreadyInSlot = Error.Conflict("SECTION_ALREADY_IN_SLOT", "The section is already scheduled in this slot.");
        public static readonly Error SectionSelfOverlap = Error.BusinessRule("SECTION_SELF_OVERLAP", "The section already meets at an overlapping time.");
        public static readonly Error RoomNotFound = Error.NotFound("SCHEDULE_ROOM_NOT_FOUND", "The room was not found.");
        public static readonly Error RoomInactive = Error.BusinessRule("ROOM_INACTIVE", "The room is not active.");
        public static readonly Error RoomCapacityExceeded = Error.BusinessRule("ROOM_CAPACITY_EXCEEDED", "The room is smaller than the section capacity.");
        public static readonly Error RoomNotAvailable = Error.BusinessRule("ROOM_NOT_AVAILABLE", "The room is not available at this time.");
        public static readonly Error RoomConflict = Error.Conflict("ROOM_CONFLICT", "The room is already used by another section at an overlapping time.");
        public static readonly Error InstructorNotAvailable = Error.BusinessRule("INSTRUCTOR_NOT_AVAILABLE", "An instructor of the section is not available at this time.");
        public static readonly Error InstructorConflict = Error.Conflict("INSTRUCTOR_CONFLICT", "An instructor of the section teaches another section at an overlapping time.");

        // Availability.
        public static readonly Error AvailabilityInvalid = Error.Validation("AVAILABILITY_INVALID", "The owner is required.");
        public static readonly Error AvailabilityNotFound = Error.NotFound("AVAILABILITY_NOT_FOUND", "The availability window was not found.");
        public static readonly Error AvailabilityOverlap = Error.Conflict("AVAILABILITY_OVERLAP", "The window overlaps another window of the same day.");
        public static readonly Error InstructorNotFound = Error.NotFound("AVAILABILITY_INSTRUCTOR_NOT_FOUND", "The instructor was not found.");
    }
}
