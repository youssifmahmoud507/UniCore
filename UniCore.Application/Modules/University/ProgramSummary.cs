using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University
{
    public sealed record ProgramSummary(Guid Id, Guid DepartmentId, string Code, string Name, DegreeType DegreeType, int DurationYears, int TotalRequiredCredits, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
}
