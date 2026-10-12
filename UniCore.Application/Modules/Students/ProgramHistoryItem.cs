using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Students
{
    public sealed record ProgramHistoryItem(Guid Id, Guid AcademicProgramId, Guid StudyPlanVersionId, Guid? AcademicLevelId, DateOnly StartDate, DateOnly? EndDate, StudentProgramStatus Status, string? Reason);
}
