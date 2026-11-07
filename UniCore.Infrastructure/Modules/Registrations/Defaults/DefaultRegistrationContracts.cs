using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Common.Results;

namespace UniCore.Infrastructure.Modules.Registrations.Defaults
{
    // Safe stand-ins until the real rules arrive. Each is replaced by registering the real class for the same interface
    // (nothing else changes). They fail OPEN only where "nothing configured" is the honest answer.

    // Until Developer 2 stores prerequisite rules: no course has prerequisites or corequisites.
    public sealed class NoPrerequisitesEvaluator : IPrerequisiteEvaluator
    {
        public Task<Result<bool>> ArePrerequisitesMetAsync(Guid studentId, Guid courseVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Ok(true));

        public Task<Result<IReadOnlyList<Guid>>> GetCorequisiteCourseIdsAsync(Guid courseVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<Guid>>.Ok([]));
    }

    // Until Week 14 (grading): no student has any recorded attempt.
    public sealed class NoCourseHistory : IStudentCourseHistory
    {
        public Task<Result<CourseAttemptSummary>> GetAttemptsAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<CourseAttemptSummary>.Ok(new CourseAttemptSummary(false, null, 0)));
    }

    // Until Week 10 (timetable): sections have no meeting times, so none can overlap.
    public sealed class NoScheduleConflictService : IScheduleConflictService
    {
        public Task<Result<bool>> HasConflictAsync(Guid studentId, Guid semesterId, Guid courseSectionId, IReadOnlyCollection<Guid> enrolledSectionIds, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<bool>.Ok(false));
    }
}
