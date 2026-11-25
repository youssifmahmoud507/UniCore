using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.StudentRequests
{
    public static class StudentRequestErrors
    {
        public static readonly Error WithdrawalNotFound = Error.NotFound("WITHDRAWAL_REQUEST_NOT_FOUND", "The withdrawal request was not found.");
        public static readonly Error LeaveNotFound = Error.NotFound("LEAVE_REQUEST_NOT_FOUND", "The leave request was not found.");
        public static readonly Error WithdrawalDataInvalid = Error.Validation("WITHDRAWAL_REQUEST_DATA_INVALID", "A withdrawal request needs a registration, one of its enrollments and a reason (up to 500 characters).");
        public static readonly Error LeaveDataInvalid = Error.Validation("LEAVE_REQUEST_DATA_INVALID", "A leave request needs a student, a reason (up to 500 characters) and a return date after the start date.");
        public static readonly Error LeaveStartInPast = Error.Validation("LEAVE_REQUEST_START_IN_PAST", "The leave cannot start before today.");
        public static readonly Error WithdrawalAlreadyPending = Error.Conflict("WITHDRAWAL_REQUEST_ALREADY_PENDING", "This enrollment already has a pending withdrawal request.");
        public static readonly Error LeaveAlreadyPending = Error.Conflict("LEAVE_REQUEST_ALREADY_PENDING", "This student already has a pending leave request.");
        public static readonly Error InvalidState = Error.BusinessRule("STUDENT_REQUEST_INVALID_STATE", "The request is not pending.");
        public static readonly Error NotYourRequest = Error.Forbidden("STUDENT_REQUEST_NOT_YOURS", "A request can only be made for your own student record.");
    }
}
