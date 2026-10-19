using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Domain.Modules.Workflow
{
    public enum WorkflowInstanceStatus
    {
        InProgress = 0,
        Completed = 1,
        Rejected = 2,
        Cancelled = 3,
        Returned = 4
    }

}
