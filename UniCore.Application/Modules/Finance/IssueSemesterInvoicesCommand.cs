namespace UniCore.Application.Modules.Finance
{
    public sealed record IssueSemesterInvoicesCommand(Guid? AcademicSemesterId, DateOnly? DueDate);

}
