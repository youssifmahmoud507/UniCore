namespace UniCore.Api.Contracts.Finance
{
    public sealed record IssueSemesterInvoicesRequest(Guid? AcademicSemesterId, DateOnly? DueDate);

}
