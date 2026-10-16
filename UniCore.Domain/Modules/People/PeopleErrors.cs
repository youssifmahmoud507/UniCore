using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.People
{
    // Error codes for the People module (person profile, employee, instructor, teaching assistant).
    public static class PeopleErrors
    {
        public static readonly Error InvalidName = Error.Validation("INVALID_PERSON_NAME", "The first and last name are required, at most 100 characters each.");
        public static readonly Error InvalidDateOfBirth = Error.Validation("INVALID_DATE_OF_BIRTH", "The date of birth must be in the past.");
        public static readonly Error InvalidGender = Error.Validation("INVALID_GENDER", "The gender is not valid.");
        public static readonly Error InvalidNationalId = Error.Validation("INVALID_NATIONAL_ID", "The national id must be 5 to 30 letters or digits.");
        public static readonly Error InvalidEmail = Error.Validation("INVALID_EMAIL", "The email address is not valid.");
        public static readonly Error InvalidPhone = Error.Validation("INVALID_PHONE", "The phone number is not valid.");
        public static readonly Error InvalidEmployeeNumber = Error.Validation("INVALID_EMPLOYEE_NUMBER", "The employee number is required, 2 to 20 characters, using only letters, digits, '-' and '_'.");
        public static readonly Error InvalidJobTitle = Error.Validation("INVALID_JOB_TITLE", "The job title must be at most 100 characters.");
        public static readonly Error InvalidSpecialization = Error.Validation("INVALID_SPECIALIZATION", "The specialization must be at most 200 characters.");
        public static readonly Error InvalidAcademicRank = Error.Validation("INVALID_ACADEMIC_RANK", "The academic rank is not valid.");
        public static readonly Error InvalidParent = Error.Validation("INVALID_PARENT", "The parent id is required.");
        public static readonly Error InvalidUser = Error.Validation("INVALID_USER", "The user id is required.");
        public static readonly Error InvalidTerminationDate = Error.Validation("INVALID_TERMINATION_DATE", "The termination date cannot be before the hire date.");

        public static readonly Error TaRequiresEmployeeOrStudent = Error.Validation("TA_REQUIRES_EMPLOYEE_OR_STUDENT", "A teaching assistant must be an employee, a student, or both.");

        public static readonly Error PersonNotFound = Error.NotFound("PERSON_NOT_FOUND", "The person was not found.");
        public static readonly Error EmployeeNotFound = Error.NotFound("EMPLOYEE_NOT_FOUND", "The employee was not found.");
        public static readonly Error InstructorNotFound = Error.NotFound("INSTRUCTOR_NOT_FOUND", "The instructor was not found.");
        public static readonly Error TeachingAssistantNotFound = Error.NotFound("TEACHING_ASSISTANT_NOT_FOUND", "The teaching assistant was not found.");
        public static readonly Error StudentNotFound = Error.NotFound("STUDENT_NOT_FOUND", "The student was not found.");

        public static readonly Error NationalIdExists = Error.Conflict("NATIONAL_ID_EXISTS", "A person with this national id already exists.");
        public static readonly Error EmployeeNumberExists = Error.Conflict("EMPLOYEE_NUMBER_EXISTS", "An employee with this number already exists.");
        public static readonly Error EmployeeAlreadyExists = Error.Conflict("EMPLOYEE_ALREADY_EXISTS", "This person is already an employee.");
        public static readonly Error InstructorExists = Error.Conflict("INSTRUCTOR_EXISTS", "This employee is already an instructor.");
        public static readonly Error TeachingAssistantExists = Error.Conflict("TEACHING_ASSISTANT_EXISTS", "This person is already a teaching assistant.");
        public static readonly Error PersonAlreadyLinked = Error.Conflict("PERSON_ALREADY_LINKED", "The person is already linked to a user account.");
        public static readonly Error UserAlreadyLinked = Error.Conflict("USER_ALREADY_LINKED", "The user account is already linked to a person.");

        public static readonly Error EmployeeNotActive = Error.BusinessRule("EMPLOYEE_NOT_ACTIVE", "The employee is not active.");
        public static readonly Error EmployeeAlreadyTerminated = Error.BusinessRule("EMPLOYEE_ALREADY_TERMINATED", "The employee is already terminated.");

        public static readonly Error AlreadyActive = Error.BusinessRule("ALREADY_ACTIVE", "The record is already active.");
        public static readonly Error AlreadyInactive = Error.BusinessRule("ALREADY_INACTIVE", "The record is already inactive.");
    }
}
