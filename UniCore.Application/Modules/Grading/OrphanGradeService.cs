using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Grading.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Grading;

namespace UniCore.Application.Modules.Grading
{
    public sealed class OrphanGradeService(IAggregateRepository<Grade> grades) : IOrphanGradeService
    {
        private readonly IAggregateRepository<Grade> _grades = grades;

        public async Task<Result<Guid?>> CancelForEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
        {
            var grade = await _grades.FirstOrDefaultAsync(new GradeOfEnrollmentSpecification(enrollmentId, tracking: true), cancellationToken);
            if (grade is null || grade.IsFinal)
            {
                return Result<Guid?>.Ok(null);
            }

            _grades.Remove(grade);

            return Result<Guid?>.Ok(grade.Id);
        }
    }
}
