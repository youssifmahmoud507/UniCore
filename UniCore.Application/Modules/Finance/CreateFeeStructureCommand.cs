namespace UniCore.Application.Modules.Finance
{
    public sealed record CreateFeeStructureCommand(Guid? AcademicProgramId, Guid? AcademicSemesterId, string? Name);

}
