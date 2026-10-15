using UniCore.Domain.Modules.University;

namespace UniCore.Api.Contracts.University
{
    public sealed record UpdateProgramRequest(string? Name, DegreeType? DegreeType, int? DurationYears, int? TotalRequiredCredits);
}
