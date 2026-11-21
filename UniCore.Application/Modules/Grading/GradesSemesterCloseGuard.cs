using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Calendar;
using UniCore.Application.Modules.Grading.Specifications;
using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Grading;

namespace UniCore.Application.Modules.Grading
{
    // A semester cannot be closed while a grade of an enrollment that is still on a roster is not Published (or Locked) yet.
    // A grade of a student who withdrew is ignored: it will never be published.
    public sealed class GradesSemesterCloseGuard(IAggregateRepository<Grade> grades, IEnrollmentReadService roster) : ISemesterCloseGuard
    {
        private readonly IAggregateRepository<Grade> _grades = grades;
        private readonly IEnrollmentReadService _roster = roster;

        public async Task<Result> CanCloseAsync(Guid semesterId, CancellationToken cancellationToken = default)
        {
            var pending = await _grades.ListAsync(new NonFinalGradesOfSemesterSpecification(semesterId), cancellationToken);

            foreach (var section in pending.GroupBy(g => g.CourseSectionId))
            {
                var entries = await _roster.GetRosterAsync(section.Key, cancellationToken);
                if (!entries.TryGetValue(out var onRoster))
                {
                    return Result.Fail(entries.Errors);
                }

                var active = onRoster.Select(e => e.EnrollmentId).ToHashSet();
                if (section.Any(g => active.Contains(g.EnrollmentId)))
                {
                    return Result.Fail(GradingErrors.UnpublishedGrades);
                }
            }

            return Result.Ok();
        }
    }
}
