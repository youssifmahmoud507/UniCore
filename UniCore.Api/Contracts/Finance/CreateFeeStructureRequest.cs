namespace UniCore.Api.Contracts.Finance
{
    public sealed record CreateFeeStructureRequest(Guid? AcademicProgramId, Guid? AcademicSemesterId, string? Name);
}
