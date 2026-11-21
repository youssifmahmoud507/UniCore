namespace UniCore.Api.Contracts.Graduation
{
    public sealed record ApproveGraduationRequest(string? Note);

    public sealed record RejectGraduationRequest(string? Reason);
}
