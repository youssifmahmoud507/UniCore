using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Assessments.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Assessments;

namespace UniCore.Application.Modules.Assessments
{
    public sealed class AssessmentReadService(IAggregateRepository<Assessment> assessments) : IAssessmentReadService
    {
        private readonly IAggregateRepository<Assessment> _assessments = assessments;

        public async Task<Result<SectionAssessmentInfo>> GetSectionAssessmentAsync(Guid courseSectionId, CancellationToken cancellationToken = default)
        {
            var assessment = await _assessments.FirstOrDefaultAsync(new LiveAssessmentOfSectionSpecification(courseSectionId, withComponents: true), cancellationToken);
            if (assessment is null || assessment.Status is not (AssessmentStatus.Published or AssessmentStatus.Closed))
            {
                return AssessmentErrors.NotFound;
            }

            return new SectionAssessmentInfo(
                assessment.Id, assessment.CourseSectionId, assessment.Status,
                [.. assessment.Components.Select(c => new AssessmentComponentInfo(c.Id, c.Name, c.Type, c.Weight, c.MaxScore, c.DueDate))]);
        }

        public Result<decimal> CalculatePercentage(SectionAssessmentInfo scheme, IReadOnlyDictionary<Guid, decimal> scores)
        {
            var total = 0m;

            foreach (var (componentId, score) in scores)
            {
                var component = scheme.Components.FirstOrDefault(c => c.ComponentId == componentId);
                if (component is null || score < 0 || score > component.MaxScore)
                {
                    return Result<decimal>.Fail(AssessmentErrors.ScoreInvalid);
                }

                total += score / component.MaxScore * component.Weight;
            }

            return Result<decimal>.Ok(decimal.Round(total, 2, MidpointRounding.AwayFromZero));
        }
    }
}
