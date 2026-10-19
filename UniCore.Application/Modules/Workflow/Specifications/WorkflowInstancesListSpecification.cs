using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow.Specifications
{
    public sealed class WorkflowInstancesListSpecification : Specification<WorkflowInstance>
    {
        public WorkflowInstancesListSpecification(PaginationQuery query, WorkflowInstanceStatus? status, Guid? requesterUserId, string? referenceType, Guid? referenceId, Guid? subjectStudentId)
        {
            SetCriteria(i => (status == null || i.Status == status) && (requesterUserId == null || i.RequesterUserId == requesterUserId) && (referenceType == null || i.ReferenceType == referenceType) && (referenceId == null || i.ReferenceId == referenceId) && (subjectStudentId == null || i.SubjectStudentId == subjectStudentId));

            ApplyOrderByDescending(i => i.StartedAt);
            ApplyPaging(query);
        }
    }
}
