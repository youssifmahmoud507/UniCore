namespace UniCore.Domain.Modules.Workflow
{
    public static class WorkflowAuditActions
    {
        public const string DefinitionCreated = "WORKFLOW_DEFINITION_CREATED";
        public const string DefinitionDeactivated = "WORKFLOW_DEFINITION_DEACTIVATED";
        public const string Started = "WORKFLOW_STARTED";
        public const string Approved = "WORKFLOW_APPROVED";
        public const string Rejected = "WORKFLOW_REJECTED";
        public const string Returned = "WORKFLOW_RETURNED";
        public const string Resubmitted = "WORKFLOW_RESUBMITTED";
        public const string Cancelled = "WORKFLOW_CANCELLED";
    }

}
