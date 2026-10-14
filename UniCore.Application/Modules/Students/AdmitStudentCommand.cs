namespace UniCore.Application.Modules.Students
{
    public sealed record AdmitStudentCommand(Guid PersonProfileId, string? Email, DateOnly AdmissionDate, DateOnly? ExpectedGraduationDate, Guid AcademicProgramId, string? ProgramCode, Guid StudyPlanVersionId, Guid AdmissionAcademicYearId, int AdmissionYear, Guid? AcademicLevelId, bool CreateAccount = true);
}
