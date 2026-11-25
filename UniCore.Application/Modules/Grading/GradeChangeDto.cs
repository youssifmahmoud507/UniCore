using UniCore.Domain.Modules.Grading;

namespace UniCore.Application.Modules.Grading
{
    public sealed record GradeChangeDto(
        Guid Id, Guid GradeId, Guid StudentId, Guid CourseSectionId, Guid AssessmentComponentId, decimal OldScore, decimal NewScore, string Reason,
        GradeChangeRequestStatus Status, Guid RequestedByUserId, DateTimeOffset RequestedAt, Guid? WorkflowInstanceId, Guid? ApprovedByUserId, DateTimeOffset? ResolvedAt, string? FailureCode);

    public static class GradeChangeMapper
    {
        public static GradeChangeDto ToDto(GradeChangeRequest r) => new(
            r.Id, r.GradeId, r.StudentId, r.CourseSectionId, r.AssessmentComponentId, r.OldScore, r.NewScore, r.Reason,
            r.Status, r.RequestedByUserId, r.RequestedAt, r.WorkflowInstanceId, r.ApprovedByUserId, r.ResolvedAt, r.FailureCode);
    }
}
