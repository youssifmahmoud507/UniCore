using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Assessments;
using UniCore.Application.Modules.Grading.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Assessments;
using UniCore.Domain.Modules.Grading;

namespace UniCore.Application.Modules.Grading
{
    // Score -> percentage -> letter -> grade points. The percentage comes from the grading scheme of the section (Assessments),
    // the letter and points from the default grade scale.
    public sealed class GradingEngine(IAssessmentReadService assessments, IAggregateRepository<GradeScale> scales)
    {
        private readonly IAssessmentReadService _assessments = assessments;
        private readonly IAggregateRepository<GradeScale> _scales = scales;

        // The scheme of the section. Entering and submitting need it Published; a correction accepts a Closed one too.
        public async Task<Result<SectionAssessmentInfo>> LoadSchemeAsync(Guid sectionId, bool mustBePublished, CancellationToken cancellationToken)
        {
            var scheme = await _assessments.GetSectionAssessmentAsync(sectionId, cancellationToken);
            if (!scheme.TryGetValue(out var info))
            {
                return GradingErrors.AssessmentNotReady;
            }

            return mustBePublished && info.Status != AssessmentStatus.Published ? GradingErrors.AssessmentNotReady : info;
        }

        public Task<Result<GradeResult>> CalculateAsync(SectionAssessmentInfo scheme, Grade grade, CancellationToken cancellationToken)
            => CalculateAsync(scheme, grade.Scores.ToDictionary(s => s.AssessmentComponentId, s => s.Score), cancellationToken);

        public async Task<Result<GradeResult>> CalculateAsync(SectionAssessmentInfo scheme, IReadOnlyDictionary<Guid, decimal> scores, CancellationToken cancellationToken)
        {
            var scale = await _scales.FirstOrDefaultAsync(new DefaultGradeScaleSpecification(), cancellationToken);
            if (scale is null)
            {
                return GradingErrors.ScaleNotFound;
            }

            var percentage = _assessments.CalculatePercentage(scheme, scores);
            if (!percentage.TryGetValue(out var value))
            {
                return Result<GradeResult>.Fail(GradingErrors.ScoreInvalid);
            }

            var band = scale.Resolve(value);
            if (!band.TryGetValue(out var found))
            {
                return Result<GradeResult>.Fail(band.Errors);
            }

            return new GradeResult(scale.Id, value, found.Letter, found.GradePoints, found.IsPassing);
        }

        public async Task<Result<GradeScale>> GetDefaultScaleAsync(CancellationToken cancellationToken)
        {
            var scale = await _scales.FirstOrDefaultAsync(new DefaultGradeScaleSpecification(), cancellationToken);

            return scale is null ? GradingErrors.ScaleNotFound : scale;
        }
    }
}
