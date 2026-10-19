namespace UniCore.Domain.Modules.Workflow
{
    public enum WorkflowActionType
    {
        Started = 0,
        Approved = 1,
        Rejected = 2,
        Returned = 3,
        Resubmitted = 4,
        Cancelled = 5
    }
}
