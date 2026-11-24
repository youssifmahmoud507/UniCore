using UniCore.Domain.Modules.StudentRequests;

namespace UniCore.Application.Modules.StudentRequests
{
    public sealed record WithdrawalRequestDto(
        Guid Id, Guid StudentId, Guid RegistrationId, Guid EnrollmentId, Guid CourseSectionId, string Reason,
        AcademicRequestStatus Status, Guid RequestedByUserId, DateTimeOffset RequestedAt, Guid? WorkflowInstanceId, Guid? ApprovedByUserId, DateTimeOffset? ResolvedAt, string? FailureCode);

    public sealed record LeaveRequestDto(
        Guid Id, Guid StudentId, DateOnly StartDate, DateOnly ExpectedReturnDate, string Reason,
        AcademicRequestStatus Status, Guid RequestedByUserId, DateTimeOffset RequestedAt, Guid? WorkflowInstanceId, Guid? ApprovedByUserId, DateTimeOffset? ResolvedAt, string? FailureCode);

    public static class StudentRequestMapper
    {
        public static WithdrawalRequestDto ToDto(WithdrawalRequest r) => new(
            r.Id, r.StudentId, r.RegistrationId, r.EnrollmentId, r.CourseSectionId, r.Reason,
            r.Status, r.RequestedByUserId, r.RequestedAt, r.WorkflowInstanceId, r.ApprovedByUserId, r.ResolvedAt, r.FailureCode);

        public static LeaveRequestDto ToDto(LeaveRequest r) => new(
            r.Id, r.StudentId, r.StartDate, r.ExpectedReturnDate, r.Reason,
            r.Status, r.RequestedByUserId, r.RequestedAt, r.WorkflowInstanceId, r.ApprovedByUserId, r.ResolvedAt, r.FailureCode);
    }
}
