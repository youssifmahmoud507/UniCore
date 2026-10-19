using UniCore.Domain.Modules.Identity;

namespace UniCore.Api.Contracts.Workflow
{
    public sealed record StartWorkflowRequest(string? DefinitionCode, string? ReferenceType, Guid? ReferenceId, Guid? SubjectStudentId, ScopeType? ScopeType, Guid? ScopeId);

}
