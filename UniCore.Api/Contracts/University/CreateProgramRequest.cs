using UniCore.Domain.Modules.University;

namespace UniCore.Api.Contracts.University
{
    // Numbers are nullable so a missing value is reported as a validation error instead of silently becoming 0.
    public sealed record CreateProgramRequest(Guid? DepartmentId, string? Code, string? Name, DegreeType? DegreeType, int? DurationYears, int? TotalRequiredCredits);
}
