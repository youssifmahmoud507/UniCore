namespace UniCore.Api.Contracts.Students
{
    public sealed record ChangeStudentProgramRequest(Guid? AcademicProgramId, Guid? StudyPlanVersionId, Guid? AcademicLevelId, DateOnly? StartDate, string? Reason);
}
