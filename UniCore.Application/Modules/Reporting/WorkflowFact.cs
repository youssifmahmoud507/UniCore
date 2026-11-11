using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Reporting
{
    public sealed record WorkflowFact(string DefinitionCode, WorkflowInstanceStatus Status, ScopeType? ScopeType, Guid? ScopeId, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt);




}
