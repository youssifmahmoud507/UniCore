using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Workflow
{
    // All of these are delivered by the outbox. The request modules react to them (side effects),
    // and Notifications tells the next approvers and the requester. The engine never calls academic code.

    public sealed record WorkflowStarted(Guid EventId, DateTimeOffset OccurredAt, Guid InstanceId, string DefinitionCode, string ReferenceType, Guid ReferenceId, Guid RequesterUserId, Guid? SubjectStudentId, string NextRole) : IDomainEvent;

}
