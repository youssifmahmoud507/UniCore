using UniCore.Application.Modules.Curriculum;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Curriculum;

namespace UniCore.Infrastructure.Modules.Curriculum
{
    // Placeholder until the Courses module (Week 7, CourseVersion) exists: any non-empty id is accepted.
    // Week 7 replaces the registration in AddCurriculumModule with a verifier that reads CourseVersion.
    public sealed class UnverifiedCourseVersionVerifier : ICourseVersionVerifier
    {
        public Task<Result> VerifyAsync(Guid courseVersionId, CancellationToken cancellationToken = default)
            => Task.FromResult(courseVersionId == Guid.Empty ? Result.Fail(CurriculumErrors.CourseVersionNotFound) : Result.Ok());
    }
}
