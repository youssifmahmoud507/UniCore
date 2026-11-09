using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Grading.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Grading;

namespace UniCore.Application.Modules.Grading
{
    public sealed class GradeReadService(IAggregateRepository<Grade> grades) : IGradeReadService
    {
        private readonly IAggregateRepository<Grade> _grades = grades;

        public async Task<Result<IReadOnlyList<GradeRecordInfo>>> GetPublishedGradesAsync(Guid studentId, Guid? semesterId = null, CancellationToken cancellationToken = default)
        {
            var items = await _grades.ListAsync(new GradesOfStudentSpecification(studentId, semesterId, finalOnly: true), cancellationToken);

            IReadOnlyList<GradeRecordInfo> records =
            [
                .. items.Where(g => g.IsFinal && g.PublishedAt is not null && g.Percentage is not null && g.Letter is not null && g.GradePoints is not null && g.IsPassing is not null)
                        .Select(g => new GradeRecordInfo(
                            g.Id, g.EnrollmentId, g.CourseSectionId, g.AcademicSemesterId, g.CourseId, g.Credits,
                            g.Percentage!.Value, g.Letter!, g.GradePoints!.Value, g.IsPassing!.Value, g.Status, g.PublishedAt!.Value))
            ];

            return Result<IReadOnlyList<GradeRecordInfo>>.Ok(records);
        }
    }
}
