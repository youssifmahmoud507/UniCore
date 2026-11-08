using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Attendance
{
    // What other modules ask the Attendance module (Examination blocks students by it; reports read it).
    // They never read its tables. No permission check here: the caller decides who may ask.
    // Errors: ATTENDANCE_SECTION_NOT_FOUND.
    public interface IAttendanceReadService
    {
        // The percentage of one student in one section from its CLOSED sessions, and the level against the policy.
        Task<Result<StudentAttendanceSummary>> GetStudentAttendanceAsync(Guid studentId, Guid courseSectionId, CancellationToken cancellationToken = default);

        // True when the policy of the student (course policy first, then program policy) is exam blocking
        // and the percentage is below its minimum. Week 12 keeps these students off the exam list.
        Task<Result<bool>> IsBlockedFromExamAsync(Guid studentId, Guid courseSectionId, CancellationToken cancellationToken = default);
    }

    // For the warnings job (Developer 2) and the handlers: recomputes every student of a section and issues a warning
    // when the level got worse than the last warning. Returns how many warnings were issued. Safe to run again.
    public interface IAttendanceWarningService
    {
        Task<Result<int>> EvaluateSectionAsync(Guid courseSectionId, CancellationToken cancellationToken = default);
    }
}
