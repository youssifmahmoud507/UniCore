namespace UniCore.Api.Contracts.Finance
{
    public sealed record GenerateChargesRequest(Guid? AcademicSemesterId, Guid? AcademicProgramId, DateOnly DueDate);



}
