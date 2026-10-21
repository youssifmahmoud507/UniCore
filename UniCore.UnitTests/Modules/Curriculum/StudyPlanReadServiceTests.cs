using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Curriculum;
using UniCore.Domain.Modules.Curriculum;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Curriculum
{
    public class StudyPlanReadServiceTests
    {
        private static readonly RequestInfo Req = CurriculumTestEnv.Request;

        private static async Task<(CurriculumTestEnv Env, Guid ProgramId, Guid PlanId, VersionDetail Active, Guid Required, Guid Elective)> NewWorldAsync()
        {
            var env = new CurriculumTestEnv();
            var program = env.SeedProgram();
            var plan = env.SeedPlan(program.Id);
            env.AsSuperAdmin();

            var draft = await env.CreateDraftAsync(plan.Id, new DateOnly(2026, 9, 1));
            var required = Guid.NewGuid();
            var elective = Guid.NewGuid();
            await env.AddCourseAsync(draft.Version.Id, required, CourseRequirementType.Required);
            await env.AddCourseAsync(draft.Version.Id, elective, CourseRequirementType.Elective);
            var active = ResultAssert.Succeeded(await env.VersionHandler.ActivateAsync(draft.Version.Id, Req));

            return (env, program.Id, plan.Id, active, required, elective);
        }

        [Fact]
        public async Task Contains_course_answers_for_the_students_version()
        {
            var (env, _, _, active, required, _) = await NewWorldAsync();

            var has = await env.ReadService.ContainsCourseAsync(active.Version.Id, required);
            Assert.True(has.IsSuccess);
            Assert.True(has.Value);
            var hasNot = await env.ReadService.ContainsCourseAsync(active.Version.Id, Guid.NewGuid());
            Assert.True(hasNot.IsSuccess);
            Assert.False(hasNot.Value);
            ResultAssert.Failed(await env.ReadService.ContainsCourseAsync(Guid.NewGuid(), required), CurriculumErrors.VersionNotFound);
        }

        [Fact]
        public async Task Get_courses_returns_the_effective_requirement_type()
        {
            var (env, _, planId, active, required, elective) = await NewWorldAsync();

            // Make the elective Required for the next version through a requirement row.
            var next = await env.CreateDraftAsync(planId, new DateOnly(2027, 9, 1), active.Version.Id);
            ResultAssert.Succeeded(await env.VersionHandler.SetRequirementAsync(next.Version.Id, new SetRequirementCommand(elective, CourseRequirementType.Required), Req));

            var courses = ResultAssert.Succeeded(await env.ReadService.GetCoursesAsync(next.Version.Id));

            Assert.Equal(2, courses.Count);
            Assert.All(courses, c => Assert.Equal(CourseRequirementType.Required, c.RequirementType));

            var original = ResultAssert.Succeeded(await env.ReadService.GetCoursesAsync(active.Version.Id));
            Assert.Equal(CourseRequirementType.Elective, original.Single(c => c.CourseVersionId == elective).RequirementType);
            Assert.Equal(CourseRequirementType.Required, original.Single(c => c.CourseVersionId == required).RequirementType);
        }

        [Fact]
        public async Task Get_version_returns_the_program_and_dates()
        {
            var (env, programId, planId, active, _, _) = await NewWorldAsync();

            var info = ResultAssert.Succeeded(await env.ReadService.GetVersionAsync(active.Version.Id));

            Assert.Equal(planId, info.StudyPlanId);
            Assert.Equal(programId, info.AcademicProgramId);
            Assert.Equal(1, info.VersionNumber);
            Assert.Equal(StudyPlanVersionStatus.Active, info.Status);
            Assert.Equal(new DateOnly(2026, 9, 1), info.EffectiveFrom);
            ResultAssert.Failed(await env.ReadService.GetVersionAsync(Guid.NewGuid()), CurriculumErrors.VersionNotFound);
        }

        [Fact]
        public async Task Effective_version_follows_the_dates_across_versions()
        {
            var (env, _, planId, active, _, _) = await NewWorldAsync();
            var next = await env.CreateDraftAsync(planId, new DateOnly(2027, 9, 1), active.Version.Id);
            ResultAssert.Succeeded(await env.VersionHandler.ActivateAsync(next.Version.Id, Req));

            var during = ResultAssert.Succeeded(await env.ReadService.GetEffectiveVersionAsync(planId, new DateOnly(2027, 1, 1)));
            var after = ResultAssert.Succeeded(await env.ReadService.GetEffectiveVersionAsync(planId, new DateOnly(2027, 9, 1)));

            Assert.Equal(active.Version.Id, during.VersionId);
            Assert.Equal(next.Version.Id, after.VersionId);
            ResultAssert.Failed(await env.ReadService.GetEffectiveVersionAsync(planId, new DateOnly(2026, 8, 31)), CurriculumErrors.VersionNotFound);
            ResultAssert.Failed(await env.ReadService.GetEffectiveVersionAsync(Guid.NewGuid(), new DateOnly(2027, 1, 1)), CurriculumErrors.PlanNotFound);
        }

        [Fact]
        public async Task Verify_version_for_program_checks_program_and_status()
        {
            var (env, programId, planId, active, _, _) = await NewWorldAsync();

            ResultAssert.Succeeded(await env.ReadService.VerifyVersionForProgramAsync(active.Version.Id, programId));
            ResultAssert.Failed(await env.ReadService.VerifyVersionForProgramAsync(active.Version.Id, Guid.NewGuid()), CurriculumErrors.ProgramMismatch);
            ResultAssert.Failed(await env.ReadService.VerifyVersionForProgramAsync(Guid.NewGuid(), programId), CurriculumErrors.VersionNotFound);

            // A draft or a retired version is not a valid placement for a student.
            var draft = await env.CreateDraftAsync(planId, new DateOnly(2027, 9, 1));
            ResultAssert.Failed(await env.ReadService.VerifyVersionForProgramAsync(draft.Version.Id, programId), CurriculumErrors.VersionNotActive);

            ResultAssert.Succeeded(await env.VersionHandler.RetireAsync(active.Version.Id, new RetireVersionCommand(new DateOnly(2027, 8, 31)), Req));
            ResultAssert.Failed(await env.ReadService.VerifyVersionForProgramAsync(active.Version.Id, programId), CurriculumErrors.VersionNotActive);
        }
    }
}
