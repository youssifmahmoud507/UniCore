namespace UniCore.Api.Contracts.StudentRequests
{
    public sealed record SubmitWithdrawalRequest(Guid? RegistrationId, Guid? EnrollmentId, string? Reason);

    public sealed record SubmitLeaveRequest(Guid? StudentId, DateOnly? StartDate, DateOnly? ExpectedReturnDate, string? Reason);
}
