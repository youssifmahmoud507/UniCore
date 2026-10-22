using UniCore.Application.Modules.Courses;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Curriculum;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Courses
{
    public class CourseCatalogReadServiceTests
    {
        private static readonly DateOnly From = CoursesTestEnv.From;
        private static readonly DateOnly Next = new(2027, 9, 1);

        private static async Task<(CoursesTestEnv Env, Guid DepartmentId, Guid CourseId, CourseVersionSummary First)> NewWorldAsync()
        {
            var env = new CoursesTestEnv();
            var department = env.SeedDepartment();
            var course = env.SeedCourse(department.Id);
            env.AsSuperAdmin();

            var first = await env.ActiveVersionAsync(course.Id, From);
            return (env, department.Id, course.Id, first);
        }

        [Fact]
        public async Task Get_course_returns_the_identity()
        {
            var (env, departmentId, courseId, _) = await NewWorldAsync();

            var info = ResultAssert.Succeeded(await env.ReadService.GetCourseAsync(courseId));

            Assert.Equal(departmentId, info.DepartmentId);
            Assert.Equal("CS101", info.Code);
            Assert.True(info.IsActive);
            ResultAssert.Failed(await env.ReadService.GetCourseAsync(Guid.NewGuid()), CoursesErrors.CourseNotFound);
        }

        [Fact]
        public async Task Get_version_returns_credits_hours_and_the_course_code()
        {
            var (env, departmentId, courseId, first) = await NewWorldAsync();

            var info = ResultAssert.Succeeded(await env.ReadService.GetVersionAsync(first.Id));

            Assert.Equal(courseId, info.CourseId);
            Assert.Equal("CS101", info.CourseCode);
            Assert.Equal(departmentId, info.DepartmentId);
            Assert.Equal(1, info.VersionNumber);
            Assert.Equal(3, info.Credits);
            Assert.Equal(2, info.LectureHours);
            Assert.Equal(2, info.PracticalHours);
            Assert.Equal(CourseVersionStatus.Active, info.Status);
            Assert.Equal(From, info.EffectiveFrom);
            ResultAssert.Failed(await env.ReadService.GetVersionAsync(Guid.NewGuid()), CoursesErrors.VersionNotFound);
        }

        [Fact]
        public async Task Effective_version_follows_the_dates_and_keeps_history()
        {
            var (env, _, courseId, first) = await NewWorldAsync();
            var draft = ResultAssert.Succeeded(await env.VersionHandler.CreateAsync(courseId, new CreateCourseVersionCommand(first.Id, null, null, 4, null, null, Next), CoursesTestEnv.Request));
            ResultAssert.Succeeded(await env.VersionHandler.ActivateAsync(draft.Id, CoursesTestEnv.Request));

            var during = ResultAssert.Succeeded(await env.ReadService.GetEffectiveVersionAsync(courseId, new DateOnly(2027, 1, 1)));
            var after = ResultAssert.Succeeded(await env.ReadService.GetEffectiveVersionAsync(courseId, Next));

            Assert.Equal(first.Id, during.CourseVersionId);
            Assert.Equal(3, during.Credits);
            Assert.Equal(draft.Id, after.CourseVersionId);
            Assert.Equal(4, after.Credits);
            ResultAssert.Failed(await env.ReadService.GetEffectiveVersionAsync(courseId, From.AddDays(-1)), CoursesErrors.VersionNotFound);
            ResultAssert.Failed(await env.ReadService.GetEffectiveVersionAsync(Guid.NewGuid(), Next), CoursesErrors.CourseNotFound);
        }

        [Fact]
        public async Task A_draft_is_never_the_effective_version()
        {
            var (env, _, courseId, first) = await NewWorldAsync();
            await env.CreateDraftAsync(courseId, Next, first.Id);

            var info = ResultAssert.Succeeded(await env.ReadService.GetEffectiveVersionAsync(courseId, Next.AddYears(1)));

            Assert.Equal(first.Id, info.CourseVersionId);
        }

        [Fact]
        public async Task The_study_plan_verifier_accepts_only_an_active_version()
        {
            var (env, _, courseId, first) = await NewWorldAsync();
            var verifier = new CourseVersionVerifier(env.Versions);
            var draft = await env.CreateDraftAsync(courseId, Next, first.Id);

            ResultAssert.Succeeded(await verifier.VerifyAsync(first.Id));
            ResultAssert.Failed(await verifier.VerifyAsync(draft.Id), CurriculumErrors.CourseVersionNotFound);
            ResultAssert.Failed(await verifier.VerifyAsync(Guid.NewGuid()), CurriculumErrors.CourseVersionNotFound);
            ResultAssert.Failed(await verifier.VerifyAsync(Guid.Empty), CurriculumErrors.CourseVersionNotFound);

            ResultAssert.Succeeded(await env.VersionHandler.ActivateAsync(draft.Id, CoursesTestEnv.Request));
            ResultAssert.Succeeded(await verifier.VerifyAsync(draft.Id));
            ResultAssert.Failed(await verifier.VerifyAsync(first.Id), CurriculumErrors.CourseVersionNotFound);
        }
    }
}
