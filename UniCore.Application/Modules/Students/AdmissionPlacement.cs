namespace UniCore.Application.Modules.Students
{
    public sealed record AdmissionPlacement(Guid AcademicProgramId, string? ProgramCode, Guid StudyPlanVersionId, Guid AdmissionAcademicYearId, int AdmissionYear, Guid? AcademicLevelId);


}
