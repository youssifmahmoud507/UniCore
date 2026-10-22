using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Curriculum;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Curriculum;

namespace UniCore.Application.Modules.Courses
{
    // The real check behind ICourseVersionVerifier (Curriculum asks, Courses answers): the version must exist and
    // be Active, so a plan never lists a draft or a retired version. Replaces the Week 6 placeholder.
    public sealed class CourseVersionVerifier(IAggregateRepository<CourseVersion> versions) : ICourseVersionVerifier
    {
        private readonly IAggregateRepository<CourseVersion> _versions = versions;

        public async Task<Result> VerifyAsync(Guid courseVersionId, CancellationToken cancellationToken = default)
        {
            if (courseVersionId == Guid.Empty)
            {
                return Result.Fail(CurriculumErrors.CourseVersionNotFound);
            }

            var version = await _versions.GetByIdAsync(courseVersionId, cancellationToken);

            return version is null || version.Status != CourseVersionStatus.Active
                ? Result.Fail(CurriculumErrors.CourseVersionNotFound)
                : Result.Ok();
        }
    }
}
