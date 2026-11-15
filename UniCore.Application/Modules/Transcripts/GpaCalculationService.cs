using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Transcripts;

namespace UniCore.Application.Modules.Transcripts
{
    // IGpaCalculationService with the default policy. No permission check: callers (Graduation, reports) authorize themselves.
    public sealed class GpaCalculationService(AcademicHistoryBuilder history) : IGpaCalculationService
    {
        private readonly AcademicHistoryBuilder _history = history;

        public async Task<Result<StudentGpaInfo>> GetCumulativeAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var built = await _history.BuildAsync(studentId, GpaPolicy.Default, cancellationToken);
            if (!built.TryGetValue(out var h))
            {
                return Result<StudentGpaInfo>.Fail(built.Errors);
            }

            return new StudentGpaInfo(studentId, TranscriptMapper.ToFigures(h.Cumulative), h.Cumulative.ReplacedAttempts);
        }

        public async Task<Result<SemesterGpaInfo>> GetSemesterAsync(Guid studentId, Guid semesterId, CancellationToken cancellationToken = default)
        {
            var built = await _history.BuildAsync(studentId, GpaPolicy.Default, cancellationToken);
            if (!built.TryGetValue(out var h))
            {
                return Result<SemesterGpaInfo>.Fail(built.Errors);
            }

            var semester = h.Semesters.FirstOrDefault(s => s.SemesterId == semesterId);
            if (semester is null)
            {
                return TranscriptErrors.NoGrades;
            }

            return new SemesterGpaInfo(studentId, semesterId, TranscriptMapper.ToFigures(semester.Semester), TranscriptMapper.ToFigures(semester.Cumulative));
        }
    }
}
