using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Grading;

namespace UniCore.Application.Modules.Grading
{
    public sealed record GradeRecordInfo(
        Guid GradeId, Guid EnrollmentId, Guid CourseSectionId, Guid AcademicSemesterId, Guid CourseId, int Credits,
        decimal Percentage, string Letter, decimal GradePoints, bool IsPassing, GradeStatus Status, DateTimeOffset PublishedAt);

    // What GPA and the transcript (Week 15) read: the Published and Locked grades of a student. Draft, Submitted, Reviewed and
    // Approved grades are never returned. No permission check: callers are authorized by their own module.
    public interface IGradeReadService
    {
        Task<Result<IReadOnlyList<GradeRecordInfo>>> GetPublishedGradesAsync(Guid studentId, Guid? semesterId = null, CancellationToken cancellationToken = default);
    }

    // What the grade change request workflow (Developer 2) calls AFTER its approval: the only way a Published or Locked grade changes.
    // The caller has already authorized the approver; the approver is recorded in the audit log.
    public interface IGradeCorrectionService
    {
        Task<Result<GradeSummary>> ApplyApprovedCorrectionAsync(Guid gradeId, Guid componentId, decimal newScore, string? reason, Guid approvedByUserId, RequestInfo request, CancellationToken cancellationToken = default);
    }
}
