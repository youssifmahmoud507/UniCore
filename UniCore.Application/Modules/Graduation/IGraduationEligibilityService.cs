using UniCore.Application.Modules.Transcripts;
using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Graduation
{
    // The answer to "can this student graduate?". Unmet holds one error per missing requirement, each with its own code
    // (GRADUATION_STUDENT_NOT_ACTIVE, GRADUATION_REQUIRED_COURSES_MISSING, GRADUATION_CREDITS_INSUFFICIENT, GRADUATION_GPA_BELOW_MINIMUM),
    // so a caller can show or act on each one. MissingRequiredCourses lists the course codes behind the first of them.
    public sealed record GraduationEligibility(
        Guid StudentId, bool IsEligible, GpaFigures Cumulative, IReadOnlyList<Error> Unmet, IReadOnlyList<string> MissingRequiredCourses);

    // Contract for Student Services (W18), reports and jobs (W22). No permission check: callers are authorized by their own module.
    // The Result fails only when the student does not exist or a read of another module fails; an ineligible student is a
    // successful result with IsEligible = false.
    public interface IGraduationEligibilityService
    {
        Task<Result<GraduationEligibility>> CheckAsync(Guid studentId, CancellationToken cancellationToken = default);
    }
}
