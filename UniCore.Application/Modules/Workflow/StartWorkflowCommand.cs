using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Workflow
{
    // ReferenceType/ReferenceId: the request this workflow belongs to (for example "WithdrawalRequest" and its id).
    // SubjectStudentId: the student the request is about. When no scope is given, the scope is the department of that student's program.
    public sealed record StartWorkflowCommand(string? DefinitionCode, string? ReferenceType, Guid? ReferenceId, Guid? SubjectStudentId = null, ScopeType? ScopeType = null, Guid? ScopeId = null);



}
