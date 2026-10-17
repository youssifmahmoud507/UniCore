namespace UniCore.Api.Contracts.Finance
{
    public sealed record CreateServiceChargeRequest(Guid? StudentId, string? FeeCode, string? Reference, DateOnly? DueDate, decimal? Amount, bool IssueInvoice = false);

}
