using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Reporting
{
    // Already counted by the database (GROUP BY).
    public sealed record WorkflowStat(WorkflowInstanceStatus Status, ScopeType? ScopeType, Guid? ScopeId, int Count);




}
