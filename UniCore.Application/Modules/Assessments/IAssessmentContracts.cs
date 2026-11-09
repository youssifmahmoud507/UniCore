using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Assessments;

namespace UniCore.Application.Modules.Assessments
{
    public sealed record AssessmentComponentInfo(Guid ComponentId, string Name, ComponentType Type, decimal Weight, decimal MaxScore, DateOnly? DueDate);

    public sealed record SectionAssessmentInfo(Guid AssessmentId, Guid CourseSectionId, AssessmentStatus Status, IReadOnlyList<AssessmentComponentInfo> Components);

    // What the Grading module (Week 14) reads: the weights and maximum scores of a section. Only a Published or Closed scheme
    // is returned; Draft and Cancelled schemes are "not found" for other modules. No permission check: callers are authorized
    // by their own module.
    public interface IAssessmentReadService
    {
        Task<Result<SectionAssessmentInfo>> GetSectionAssessmentAsync(Guid courseSectionId, CancellationToken cancellationToken = default);

        // The percentage of the final grade of a set of scores (0-100): sum(score / max * weight). A missing component counts as 0.
        // Fails when a score is above the maximum or negative, or names a component that is not in the scheme.
        Result<decimal> CalculatePercentage(SectionAssessmentInfo scheme, IReadOnlyDictionary<Guid, decimal> scores);
    }
}
