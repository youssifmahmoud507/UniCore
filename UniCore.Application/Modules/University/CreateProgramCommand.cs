using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University
{
    public sealed record CreateProgramCommand(Guid DepartmentId, string? Code, string? Name, DegreeType? DegreeType, int DurationYears, int TotalRequiredCredits);
}
