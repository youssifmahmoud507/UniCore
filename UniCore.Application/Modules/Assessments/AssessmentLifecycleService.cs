using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Assessments.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Assessments;

namespace UniCore.Application.Modules.Assessments
{
    public sealed class AssessmentLifecycleService(IAggregateRepository<Assessment> assessments, IClock clock) : IAssessmentLifecycle
    {
        private readonly IAggregateRepository<Assessment> _assessments = assessments;
        private readonly IClock _clock = clock;

        public async Task<Result> CloseForGradingAsync(Guid assessmentId, CancellationToken cancellationToken = default)
        {
            var assessment = await _assessments.FirstOrDefaultAsync(new AssessmentByIdSpecification(assessmentId, tracking: true), cancellationToken);
            if (assessment is null)
            {
                return Result.Fail(AssessmentErrors.NotFound);
            }

            if (assessment.Status == AssessmentStatus.Closed)
            {
                return Result.Ok();
            }

            var closed = assessment.Close(_clock.UtcNow);
            if (closed.IsFailure)
            {
                return closed;
            }

            _assessments.Update(assessment);

            return Result.Ok();
        }
    }
}
