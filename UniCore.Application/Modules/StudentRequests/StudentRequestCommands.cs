namespace UniCore.Application.Modules.StudentRequests
{
    public sealed record SubmitWithdrawalCommand(Guid? RegistrationId, Guid? EnrollmentId, string? Reason);

    public sealed record SubmitLeaveCommand(Guid? StudentId, DateOnly? StartDate, DateOnly? ExpectedReturnDate, string? Reason);
}
