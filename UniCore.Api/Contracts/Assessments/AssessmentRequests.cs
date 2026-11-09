using UniCore.Domain.Modules.Assessments;

namespace UniCore.Api.Contracts.Assessments
{
    // Numbers and dates are nullable so a missing value is reported as a validation error instead of silently becoming a default.
    // Due date "yyyy-MM-dd".
    public sealed record CreateAssessmentRequest(Guid? CourseSectionId);

    public sealed record ComponentRequest(string? Name, ComponentType? Type, decimal? Weight, decimal? MaxScore, DateOnly? DueDate);
}
