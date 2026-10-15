using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University
{
    public sealed record UpdateProgramCommand(string? Name, DegreeType? DegreeType, int DurationYears, int TotalRequiredCredits);
}
