using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Assessments;
using UniCore.Application.Modules.Grading.Specifications;
using UniCore.Domain.Modules.Grading;

namespace UniCore.Application.Modules.Grading
{
    // Tells Assessments whether grades exist for a scheme (so it can refuse to cancel it).
    public sealed class GradeAssessmentUsage(IAggregateRepository<Grade> grades) : IAssessmentGradeUsage
    {
        private readonly IAggregateRepository<Grade> _grades = grades;

        public Task<bool> HasGradesAsync(Guid assessmentId, CancellationToken cancellationToken = default)
            => _grades.AnyAsync(new GradesOfAssessmentSpecification(assessmentId), cancellationToken);
    }
}
