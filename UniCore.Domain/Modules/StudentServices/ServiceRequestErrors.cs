using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.StudentServices
{
    public static class ServiceRequestErrors
    {
        public static readonly Error StudentNotFound = Error.NotFound("SERVICE_REQUEST_STUDENT_NOT_FOUND", "The student was not found.");
        public static readonly Error NotFound = Error.NotFound("SERVICE_REQUEST_NOT_FOUND", "The request was not found.");
        public static readonly Error Invalid = Error.Validation("SERVICE_REQUEST_INVALID", "The request data is not valid.");
        public static readonly Error TextRequired = Error.Validation("SERVICE_REQUEST_TEXT_REQUIRED", "The description of the request is required.");
        public static readonly Error TextTooLong = Error.Validation("SERVICE_REQUEST_TEXT_TOO_LONG", "A text of the request is too long.");
        public static readonly Error NoteRequired = Error.Validation("SERVICE_REQUEST_NOTE_REQUIRED", "A reason is required.");
        public static readonly Error AlreadyPending = Error.Conflict("SERVICE_REQUEST_ALREADY_PENDING", "The student already has an open request of this kind.");
        public static readonly Error InvalidState = Error.BusinessRule("SERVICE_REQUEST_INVALID_STATE", "The request is not in a state that allows this.");
        public static readonly Error WorkflowAlreadyAttached = Error.BusinessRule("SERVICE_REQUEST_WORKFLOW_ATTACHED", "The request already belongs to a workflow.");
        public static readonly Error ActorRequired = Error.Forbidden("SERVICE_REQUEST_ACTOR_REQUIRED", "A signed-in user is required.");

        public static readonly Error GradeNotFound = Error.NotFound("SERVICE_REQUEST_GRADE_NOT_FOUND", "The published grade was not found for this student.");
    }
}
