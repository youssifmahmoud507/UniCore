using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Grading.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Grading;

namespace UniCore.Application.Modules.Grading
{
    // The only way a Published or Locked grade changes. Called by the grade change request workflow (Developer 2) after its own
    // approval; the approver is the actor of the audit entry. The status stays; GradeCorrected is raised for GPA and notifications.
    public sealed class GradeCorrectionService(
        GradingEngine engine, IAggregateRepository<Grade> grades, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit) : IGradeCorrectionService
    {
        private readonly GradingEngine _engine = engine;
        private readonly IAggregateRepository<Grade> _grades = grades;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result<GradeSummary>> ApplyApprovedCorrectionAsync(
            Guid gradeId, Guid componentId, decimal newScore, string? reason, Guid approvedByUserId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var grade = await _grades.FirstOrDefaultAsync(new GradeByIdSpecification(gradeId, tracking: true), cancellationToken);
            if (grade is null)
            {
                return GradingErrors.NotFound;
            }

            if (!grade.IsFinal)
            {
                return GradingErrors.InvalidState;
            }

            var scheme = await _engine.LoadSchemeAsync(grade.CourseSectionId, mustBePublished: false, cancellationToken);
            if (!scheme.TryGetValue(out var info))
            {
                return Result<GradeSummary>.Fail(scheme.Errors);
            }

            var component = info.Components.FirstOrDefault(c => c.ComponentId == componentId);
            if (component is null || grade.Scores.All(s => s.AssessmentComponentId != componentId))
            {
                return GradingErrors.CorrectionInvalid;
            }

            if (newScore < 0 || newScore > component.MaxScore)
            {
                return GradingErrors.ScoreInvalid;
            }

            var scores = grade.Scores.ToDictionary(s => s.AssessmentComponentId, s => s.Score);
            var before = $"Status={grade.Status};Percentage={grade.Percentage};Letter={grade.Letter};Score={scores[componentId]}";
            scores[componentId] = newScore;

            var result = await _engine.CalculateAsync(info, scores, cancellationToken);
            if (!result.TryGetValue(out var calculated))
            {
                return Result<GradeSummary>.Fail(result.Errors);
            }

            var corrected = grade.Correct(componentId, newScore, calculated, reason, _clock.UtcNow);
            if (corrected.IsFailure)
            {
                return Result<GradeSummary>.Fail(corrected.Errors);
            }

            _grades.Update(grade);

            var entry = new AuditEntry(GradingAuditActions.Corrected, approvedByUserId, "Grade", grade.Id.ToString(), request.IpAddress, request.UserAgent, before, $"Status={grade.Status};Percentage={grade.Percentage};Letter={grade.Letter};Score={newScore};Reason={reason?.Trim()}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<GradeSummary>.Fail(committed.Errors) : GradingMapper.ToSummary(grade);
        }
    }
}
