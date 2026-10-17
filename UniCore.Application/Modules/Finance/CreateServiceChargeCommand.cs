namespace UniCore.Application.Modules.Finance
{
    public sealed record CreateServiceChargeCommand(Guid? StudentId, string? FeeCode, string? Reference, DateOnly? DueDate, decimal? Amount, bool IssueInvoice);


}
