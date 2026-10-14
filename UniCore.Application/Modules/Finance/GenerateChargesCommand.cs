namespace UniCore.Application.Modules.Finance
{
    // AcademicProgramId = null means every program.
    public sealed record GenerateChargesCommand(Guid? AcademicSemesterId, Guid? AcademicProgramId, DateOnly DueDate);



}
