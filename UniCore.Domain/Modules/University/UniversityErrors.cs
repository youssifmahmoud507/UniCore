using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.University
{
    // Error codes for the University module (university, campus, building, room, faculty, department).
    public static class UniversityErrors
    {
        // Field validation. The same codes are used by every entity of the module because the rule is the same.
        public static readonly Error InvalidCode = Error.Validation("INVALID_CODE", "The code is required, 2 to 20 characters, using only letters, digits, '-' and '_'.");
        public static readonly Error InvalidName = Error.Validation("INVALID_NAME", "The name is required and must be 2 to 200 characters.");
        public static readonly Error InvalidDescription = Error.Validation("INVALID_DESCRIPTION", "The description must be at most 1000 characters.");
        public static readonly Error InvalidEmail = Error.Validation("INVALID_EMAIL", "The email address is not valid.");
        public static readonly Error InvalidPhone = Error.Validation("INVALID_PHONE", "The phone number is not valid.");
        public static readonly Error InvalidWebsite = Error.Validation("INVALID_WEBSITE", "The website must be an absolute http or https address.");
        public static readonly Error InvalidTimeZone = Error.Validation("INVALID_TIME_ZONE", "The time zone id is not valid.");
        public static readonly Error AddressRequired = Error.Validation("ADDRESS_REQUIRED", "An address is required.");
        public static readonly Error InvalidRoomCapacity = Error.Validation("INVALID_ROOM_CAPACITY", "The room capacity must be between 1 and 10000.");
        public static readonly Error InvalidRoomType = Error.Validation("INVALID_ROOM_TYPE", "The room type is not valid.");
        public static readonly Error InvalidParent = Error.Validation("INVALID_PARENT", "The parent id is required.");

        // Academic program fields.
        public static readonly Error InvalidDegreeType = Error.Validation("INVALID_DEGREE_TYPE", "The degree type is not valid.");
        public static readonly Error InvalidDuration = Error.Validation("INVALID_DURATION", "The duration must be between 1 and 10 years.");
        public static readonly Error InvalidRequiredCredits = Error.Validation("INVALID_REQUIRED_CREDITS", "The total required credits must be between 1 and 400.");

        // Lookups. Checked by the application layer, not by the entities.
        public static readonly Error UniversityNotFound = Error.NotFound("UNIVERSITY_NOT_FOUND", "The university was not found.");
        public static readonly Error CampusNotFound = Error.NotFound("CAMPUS_NOT_FOUND", "The campus was not found.");
        public static readonly Error FacultyNotFound = Error.NotFound("FACULTY_NOT_FOUND", "The faculty was not found.");
        public static readonly Error DepartmentNotFound = Error.NotFound("DEPARTMENT_NOT_FOUND", "The department was not found.");
        public static readonly Error ProgramNotFound = Error.NotFound("PROGRAM_NOT_FOUND", "The academic program was not found.");
        public static readonly Error ParentInactive = Error.BusinessRule("PARENT_INACTIVE", "The parent record is inactive, so nothing new can be added under it.");
        public static readonly Error ProgramInactive = Error.BusinessRule("PROGRAM_INACTIVE", "The academic program is inactive.");
        public static readonly Error ProgramCodeMismatch = Error.Validation("PROGRAM_CODE_MISMATCH", "The program code does not match the program.");
        public static readonly Error TimeZoneUnknown = Error.Validation("TIME_ZONE_UNKNOWN", "The time zone id is not known on this server.");

        // Activation state.
        public static readonly Error AlreadyActive = Error.BusinessRule("ALREADY_ACTIVE", "The record is already active.");
        public static readonly Error AlreadyInactive = Error.BusinessRule("ALREADY_INACTIVE", "The record is already inactive.");

        // A parent cannot be deactivated while it still has active children.
        public static readonly Error UniversityHasActiveChildren = Error.BusinessRule("UNIVERSITY_HAS_ACTIVE_CHILDREN", "The university still has active faculties or campuses.");
        public static readonly Error FacultyHasActiveDepartments = Error.BusinessRule("FACULTY_HAS_ACTIVE_DEPARTMENTS", "The faculty still has active departments.");
        public static readonly Error DepartmentHasActivePrograms = Error.BusinessRule("DEPARTMENT_HAS_ACTIVE_PROGRAMS", "The department still has active programs.");
        public static readonly Error ProgramHasActiveStudents = Error.BusinessRule("PROGRAM_HAS_ACTIVE_STUDENTS", "The program still has students studying in it.");
        public static readonly Error CampusHasActiveBuildings = Error.BusinessRule("CAMPUS_HAS_ACTIVE_BUILDINGS", "The campus still has active buildings.");
        public static readonly Error BuildingHasActiveRooms = Error.BusinessRule("BUILDING_HAS_ACTIVE_ROOMS", "The building still has active rooms.");
    }
}
