using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Curriculum
{
    // Contract with the Courses module (Week 7, CourseVersion): does this course version exist and may it be
    // used in a plan (not retired)? Until Courses exists, the Infrastructure default accepts any non-empty id
    // (UnverifiedCourseVersionVerifier, the same idea as the placement verifier had). Returns
    // STUDY_PLAN_COURSE_VERSION_NOT_FOUND when the course version is unknown.
    public interface ICourseVersionVerifier
    {
        Task<Result> VerifyAsync(Guid courseVersionId, CancellationToken cancellationToken = default);
    }
}
