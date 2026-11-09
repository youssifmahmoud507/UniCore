using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Examination
{
    public static class ExaminationErrors
    {
        // Sessions.
        public static readonly Error SessionInvalid = Error.Validation("EXAM_SESSION_INVALID", "The semester, a name (up to 200 characters), a valid type and a start date not after the end date are required.");
        public static readonly Error SessionNotFound = Error.NotFound("EXAM_SESSION_NOT_FOUND", "The exam session was not found.");
        public static readonly Error SessionNameExists = Error.Conflict("EXAM_SESSION_NAME_EXISTS", "The semester already has an exam session with this name.");
        public static readonly Error SessionClosed = Error.BusinessRule("EXAM_SESSION_CLOSED", "The exam session is closed.");
        public static readonly Error SessionOutsideWindow = Error.BusinessRule("EXAM_SESSION_OUTSIDE_WINDOW", "The session dates must be inside the exam window of the semester for this exam type.");
        public static readonly Error SemesterNotFound = Error.NotFound("EXAM_SEMESTER_NOT_FOUND", "The semester was not found.");
        public static readonly Error SemesterClosed = Error.BusinessRule("EXAM_SEMESTER_CLOSED", "The semester is closed, so exams cannot change.");

        // Exams.
        public static readonly Error ExamInvalid = Error.Validation("EXAM_INVALID", "The section, the session, a valid type, a maximum score above 0 and a duration of 15 to 480 minutes are required.");
        public static readonly Error ExamNotFound = Error.NotFound("EXAM_NOT_FOUND", "The exam was not found.");
        public static readonly Error ExamExists = Error.Conflict("EXAM_EXISTS", "The section already has an active exam of this type.");
        public static readonly Error ExamInvalidState = Error.BusinessRule("EXAM_INVALID_STATE", "The exam cannot be changed in its current state.");
        public static readonly Error SectionNotFound = Error.NotFound("EXAM_SECTION_NOT_FOUND", "The course section was not found.");
        public static readonly Error SectionSemesterMismatch = Error.BusinessRule("EXAM_SECTION_SEMESTER_MISMATCH", "The section belongs to another semester than the exam session.");
        public static readonly Error TypeMismatch = Error.BusinessRule("EXAM_TYPE_MISMATCH", "The exam type must match the type of its session.");

        // Schedule.
        public static readonly Error ScheduleInvalid = Error.Validation("EXAM_SCHEDULE_INVALID", "A date and a start time before the end time (whole minutes) are required.");
        public static readonly Error DurationTooLong = Error.BusinessRule("EXAM_DURATION_EXCEEDS_SLOT", "The time range is shorter than the duration of the exam.");
        public static readonly Error DateOutsideSession = Error.BusinessRule("EXAM_DATE_OUTSIDE_SESSION", "The exam date is outside the dates of its session.");
        public static readonly Error DateOutsideWindow = Error.BusinessRule("EXAM_DATE_OUTSIDE_WINDOW", "The exam date is outside the exam window of the semester.");
        public static readonly Error NotScheduled = Error.BusinessRule("EXAM_NOT_SCHEDULED", "The exam has no date and time yet.");

        // Conflicts.
        public static readonly Error StudentConflict = Error.Conflict("EXAM_STUDENT_CONFLICT", "A student of this exam has another exam at an overlapping time.");
        public static readonly Error RoomConflict = Error.Conflict("EXAM_ROOM_CONFLICT", "A room of this exam is used by another exam at an overlapping time.");
        public static readonly Error InvigilatorConflict = Error.Conflict("EXAM_INVIGILATOR_CONFLICT", "An invigilator of this exam is invigilating another exam at an overlapping time.");

        // Rooms and invigilators.
        public static readonly Error RoomNotFound = Error.NotFound("EXAM_ROOM_NOT_FOUND", "The room was not found.");
        public static readonly Error RoomInactive = Error.BusinessRule("EXAM_ROOM_INACTIVE", "The room is not active.");
        public static readonly Error RoomExists = Error.Conflict("EXAM_ROOM_EXISTS", "The room is already used by this exam.");
        public static readonly Error ExamRoomNotFound = Error.NotFound("EXAM_ROOM_ASSIGNMENT_NOT_FOUND", "The room is not used by this exam.");
        public static readonly Error CapacityNotEnough = Error.BusinessRule("EXAM_CAPACITY_NOT_ENOUGH", "The capacity of the rooms is less than the number of registered students.");
        public static readonly Error InvigilatorNotFound = Error.NotFound("EXAM_INVIGILATOR_NOT_FOUND", "The invigilator was not found.");
        public static readonly Error InvigilatorInactive = Error.BusinessRule("EXAM_INVIGILATOR_INACTIVE", "The invigilator is not active.");
        public static readonly Error InvigilatorExists = Error.Conflict("EXAM_INVIGILATOR_EXISTS", "The invigilator is already assigned to this exam.");
        public static readonly Error ChiefExists = Error.Conflict("EXAM_CHIEF_EXISTS", "The room already has a chief invigilator.");
        public static readonly Error InvigilatorInvalid = Error.Validation("EXAM_INVIGILATOR_INVALID", "A room, an invigilator and a valid role are required.");
        public static readonly Error ChiefMissing = Error.BusinessRule("EXAM_CHIEF_MISSING", "Every room needs a chief invigilator.");
        public static readonly Error NoRooms = Error.BusinessRule("EXAM_NO_ROOMS", "The exam needs at least one room.");
        public static readonly Error StudentNotFound = Error.NotFound("EXAM_STUDENT_NOT_FOUND", "The student was not found.");
    }
}
