namespace UniCore.Application.Modules.Students
{
    public sealed record StudentDetail(StudentSummary Summary, Guid? AdmissionAcademicYearId, IReadOnlyList<ProgramHistoryItem> ProgramHistory, IReadOnlyList<StatusHistoryItem> StatusHistory);
}
