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

        // Activation state.
        public static readonly Error AlreadyActive = Error.BusinessRule("ALREADY_ACTIVE", "The record is already active.");
        public static readonly Error AlreadyInactive = Error.BusinessRule("ALREADY_INACTIVE", "The record is already inactive.");

        // A parent cannot be deactivated while it still has active children.
        public static readonly Error UniversityHasActiveChildren = Error.BusinessRule("UNIVERSITY_HAS_ACTIVE_CHILDREN", "The university still has active faculties or campuses.");
        public static readonly Error FacultyHasActiveDepartments = Error.BusinessRule("FACULTY_HAS_ACTIVE_DEPARTMENTS", "The faculty still has active departments.");
        public static readonly Error DepartmentHasActivePrograms = Error.BusinessRule("DEPARTMENT_HAS_ACTIVE_PROGRAMS", "The department still has active programs.");
        public static readonly Error CampusHasActiveBuildings = Error.BusinessRule("CAMPUS_HAS_ACTIVE_BUILDINGS", "The campus still has active buildings.");
        public static readonly Error BuildingHasActiveRooms = Error.BusinessRule("BUILDING_HAS_ACTIVE_ROOMS", "The building still has active rooms.");
    }
}
