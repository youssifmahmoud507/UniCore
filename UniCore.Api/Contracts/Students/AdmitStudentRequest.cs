namespace UniCore.Api.Contracts.Students
{
    // Nullable on purpose: a missing field becomes a proper validation error from the domain.
    public sealed record AdmitStudentRequest(Guid? PersonProfileId, string? Email, DateOnly? AdmissionDate, DateOnly? ExpectedGraduationDate, Guid? AcademicProgramId, string? ProgramCode, Guid? StudyPlanVersionId, Guid? AdmissionAcademicYearId, int? AdmissionYear, Guid? AcademicLevelId, bool CreateAccount = true);
}
