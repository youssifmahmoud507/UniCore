using UniCore.Application.Modules.Assessments;
using UniCore.Domain.Modules.Assessments;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Assessments
{
    public class AssessmentGradeGuardTests
    {
        private static readonly UniCore.Application.Common.Abstractions.RequestInfo Request = AssessmentsTestEnv.Request;

        private static async Task<(AssessmentsTestEnv Env, AssessmentSummary Scheme)> PublishedAsync()
        {
            var env = new AssessmentsTestEnv();
            var section = env.SeedSection();
            var scheme = await env.CreateOkAsync(section);
            scheme = await env.FillToHundredAsync(scheme);
            env.AsHead();
            Assert.True((await env.Handler.PublishAsync(scheme.Id, Request)).IsSuccess);

            return (env, scheme);
        }

        [Fact]
        public async Task A_scheme_with_grades_cannot_be_cancelled()
        {
            var (env, scheme) = await PublishedAsync();
            env.GradeUsage.HasGrades = true;
            env.AsHead();

            var result = await env.Handler.CancelAsync(scheme.Id, Request);

            Assert.Equal(AssessmentErrors.HasGrades.Code, result.Error?.Code);
            Assert.Equal(AssessmentStatus.Published, env.Assessments.Items.Single().Status);
        }

        [Fact]
        public async Task A_scheme_without_grades_is_still_cancelled()
        {
            var (env, scheme) = await PublishedAsync();
            env.AsHead();

            var result = await env.Handler.CancelAsync(scheme.Id, Request);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(AssessmentStatus.Cancelled, env.Assessments.Items.Single().Status);
        }

        [Fact]
        public async Task Closing_for_grading_closes_a_published_scheme_and_is_safe_to_repeat()
        {
            var (env, scheme) = await PublishedAsync();

            ResultAssert.Succeeded(await env.Lifecycle.CloseForGradingAsync(scheme.Id));
            Assert.Equal(AssessmentStatus.Closed, env.Assessments.Items.Single().Status);

            ResultAssert.Succeeded(await env.Lifecycle.CloseForGradingAsync(scheme.Id));
            Assert.Equal(AssessmentStatus.Closed, env.Assessments.Items.Single().Status);
        }

        [Fact]
        public async Task Closing_a_draft_or_unknown_scheme_fails()
        {
            var env = new AssessmentsTestEnv();
            var draft = await env.CreateOkAsync(env.SeedSection());

            Assert.True((await env.Lifecycle.CloseForGradingAsync(draft.Id)).IsFailure);
            Assert.True((await env.Lifecycle.CloseForGradingAsync(Guid.NewGuid())).IsFailure);
        }

        [Fact]
        public async Task A_scheme_is_read_by_its_id_while_published_or_closed_but_not_as_a_draft_or_when_cancelled()
        {
            var env = new AssessmentsTestEnv();
            var draft = await env.CreateOkAsync(env.SeedSection());
            Assert.True((await env.Reader.GetAssessmentAsync(draft.Id)).IsFailure);

            var (env2, scheme) = await PublishedAsync();
            Assert.True((await env2.Reader.GetAssessmentAsync(scheme.Id)).IsSuccess);

            ResultAssert.Succeeded(await env2.Lifecycle.CloseForGradingAsync(scheme.Id));
            Assert.True((await env2.Reader.GetAssessmentAsync(scheme.Id)).IsSuccess);

            Assert.True((await env2.Reader.GetAssessmentAsync(Guid.NewGuid())).IsFailure);
        }
    }
}
