using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Workflow
{
    public sealed record WorkflowReturned(Guid EventId, DateTimeOffset OccurredAt, Guid InstanceId, string DefinitionCode, string ReferenceType, Guid ReferenceId, Guid RequesterUserId, Guid? SubjectStudentId, Guid DecidedByUserId, string Comment) : IDomainEvent;

}
