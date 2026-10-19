using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Workflow
{
    public static class WorkflowErrors
    {
        public static readonly Error DefinitionNotFound = Error.NotFound("WORKFLOW_DEFINITION_NOT_FOUND", "The workflow definition was not found.");
        public static readonly Error DefinitionInactive = Error.BusinessRule("WORKFLOW_DEFINITION_INACTIVE", "The workflow definition is not active.");
        public static readonly Error DefinitionDataInvalid = Error.Validation("WORKFLOW_DEFINITION_DATA_INVALID", "The workflow definition data is invalid.");
        public static readonly Error DefinitionAlreadyExists = Error.Conflict("WORKFLOW_DEFINITION_ALREADY_EXISTS", "This workflow version already exists.");
        public static readonly Error InstanceNotFound = Error.NotFound("WORKFLOW_INSTANCE_NOT_FOUND", "The workflow instance was not found.");
        public static readonly Error RequestDataInvalid = Error.Validation("WORKFLOW_REQUEST_DATA_INVALID", "The workflow request data is invalid.");
        public static readonly Error InvalidState = Error.BusinessRule("WORKFLOW_INVALID_STATE", "The workflow is not in a state that allows this action.");
        public static readonly Error NotCurrentApprover = Error.Forbidden("WORKFLOW_NOT_CURRENT_APPROVER", "You are not an approver of the current step.");
        public static readonly Error NotRequester = Error.Forbidden("WORKFLOW_NOT_REQUESTER", "Only the person who made the request can do this.");
        public static readonly Error SelfApproval = Error.Forbidden("WORKFLOW_SELF_APPROVAL", "You cannot act on your own request.");
        public static readonly Error CommentRequired = Error.Validation("WORKFLOW_COMMENT_REQUIRED", "A comment is required for this action.");
        public static readonly Error AlreadyActive = Error.Conflict("WORKFLOW_ALREADY_ACTIVE", "This request already has an active workflow.");
    }

}
