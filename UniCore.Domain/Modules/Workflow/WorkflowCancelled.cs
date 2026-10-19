using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Workflow
{
    public sealed record WorkflowCancelled(Guid EventId, DateTimeOffset OccurredAt, Guid InstanceId, string DefinitionCode, string ReferenceType, Guid ReferenceId, Guid RequesterUserId, Guid? SubjectStudentId, Guid CancelledByUserId) : IDomainEvent;

}
