using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Curriculum
{
    // Contract with the Courses module: does this course version exist and may it be used in a plan (it must be
    // Active)? Answered by CourseVersionVerifier in the Courses module. Returns
    // STUDY_PLAN_COURSE_VERSION_NOT_FOUND when the course version is unknown or not Active.
    public interface ICourseVersionVerifier
    {
        Task<Result> VerifyAsync(Guid courseVersionId, CancellationToken cancellationToken = default);
    }
}
