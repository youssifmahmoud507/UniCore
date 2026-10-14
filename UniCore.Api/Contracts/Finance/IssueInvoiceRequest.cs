namespace UniCore.Api.Contracts.Finance
{
    public sealed record IssueInvoiceRequest(Guid? StudentId, Guid? AcademicSemesterId, DateOnly? DueDate);



}
