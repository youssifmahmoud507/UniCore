using UniCore.Domain.Modules.Identity;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // Grade entry and the approval chain: Draft -> Submitted -> Reviewed -> Approved -> Published -> Locked.
    // The admin (SuperAdmin) enters and publishes; a reviewer and an approver are separate users, because nobody may
    // review or approve what they entered or reviewed.
    [Collection(ApiCollection.Name)]
    public sealed class GradingApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static IEnumerable<string> StatusesOf(ApiResponse sheet)
            => sheet.Prop("items").EnumerateArray().Select(g => g.GetProperty("status").GetString() ?? string.Empty);

        [Fact]
        public async Task A_grade_goes_through_the_whole_chain_and_is_then_locked()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 3);
            var reviewer = await GradingScenario.ReviewerAsync(_api);
            var approver = await GradingScenario.ApproverAsync(_api);

            await scenario.EnterAllAsync(_api, admin);

            var submitted = await scenario.StepAsync(_api, admin, "submit");
            Assert.Equal(200, submitted.Code);
            Assert.Equal("Submitted", submitted.Prop("status").GetString());
            Assert.Equal(3, submitted.Prop("count").GetInt32());

            var reviewed = await scenario.StepAsync(_api, reviewer, "review");
            Assert.Equal(200, reviewed.Code);
            Assert.Equal("Reviewed", reviewed.Prop("status").GetString());

            var approved = await scenario.StepAsync(_api, approver, "approve");
            Assert.Equal(200, approved.Code);
            Assert.Equal("Approved", approved.Prop("status").GetString());

            var published = await scenario.StepAsync(_api, admin, "publish");
            Assert.Equal(200, published.Code);
            Assert.Equal("Published", published.Prop("status").GetString());

            var locked = await scenario.StepAsync(_api, admin, "lock");
            Assert.Equal(200, locked.Code);
            Assert.Equal("Locked", locked.Prop("status").GetString());

            var sheet = await scenario.SheetAsync(_api, admin);
            Assert.Equal(200, sheet.Code);
            Assert.All(StatusesOf(sheet), status => Assert.Equal("Locked", status));
        }

        [Fact]
        public async Task The_percentage_comes_from_the_weights_and_the_scores()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);

            // Quiz 20 of 20 weighs 40, Final 50 of 100 weighs 30: 70 percent.
            var entered = await scenario.EnterAsync(_api, admin, scenario.Students[0].Id, quiz: 20m, final: 50m);

            Assert.Equal(200, entered.Code);
            Assert.Equal("Draft", entered.Prop("status").GetString());
            Assert.Equal(70m, entered.Prop("percentage").GetDecimal());
            Assert.Equal(2, entered.Prop("scores").GetArrayLength());
        }

        [Fact]
        public async Task A_section_with_a_missing_grade_is_not_submitted_and_nothing_is_half_submitted()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 3);

            // Only two of the three students have a grade.
            Assert.Equal(200, (await scenario.EnterAsync(_api, admin, scenario.Students[0].Id)).Code);
            Assert.Equal(200, (await scenario.EnterAsync(_api, admin, scenario.Students[1].Id)).Code);

            var submit = await scenario.StepAsync(_api, admin, "submit");
            Assert.Equal(422, submit.Code);
            Assert.Equal("GRADE_SECTION_INCOMPLETE", submit.ErrorCode);

            var sheet = await scenario.SheetAsync(_api, admin);
            Assert.All(StatusesOf(sheet), status => Assert.Equal("Draft", status));
        }

        [Fact]
        public async Task Nobody_reviews_or_approves_what_they_entered_or_reviewed()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);
            var reviewerWhoApproves = await GradingScenario.ReviewerAsync(_api, alsoApprover: true);

            await scenario.EnterAllAsync(_api, admin);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);

            // The admin entered the grades, so the admin cannot review them.
            var selfReview = await scenario.StepAsync(_api, admin, "review");
            Assert.Equal(422, selfReview.Code);
            Assert.Equal("GRADE_SEPARATION_OF_DUTIES", selfReview.ErrorCode);

            Assert.Equal(200, (await scenario.StepAsync(_api, reviewerWhoApproves, "review")).Code);

            // The reviewer holds Approve too, but reviewed the same grades.
            var selfApprove = await scenario.StepAsync(_api, reviewerWhoApproves, "approve");
            Assert.Equal(422, selfApprove.Code);
            Assert.Equal("GRADE_SEPARATION_OF_DUTIES", selfApprove.ErrorCode);
        }

        [Fact]
        public async Task Steps_cannot_be_skipped()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);
            var approver = await GradingScenario.ApproverAsync(_api);

            await scenario.EnterAllAsync(_api, admin);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);

            // Submitted grades must be reviewed before they can be approved.
            var early = await scenario.StepAsync(_api, approver, "approve");
            Assert.Equal(422, early.Code);
            Assert.Contains(early.ErrorCode, new[] { "GRADE_INVALID_STATE", "GRADE_NOTHING_TO_PROCESS" });

            // And nothing can be published before it is approved.
            var publish = await scenario.StepAsync(_api, admin, "publish");
            Assert.Equal(422, publish.Code);
            Assert.Contains(publish.ErrorCode, new[] { "GRADE_INVALID_STATE", "GRADE_NOTHING_TO_PROCESS" });
        }

        [Fact]
        public async Task A_submitted_section_goes_back_to_draft_with_a_reason()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 2);
            var reviewer = await GradingScenario.ReviewerAsync(_api);

            await scenario.EnterAllAsync(_api, admin);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);

            var noReason = await scenario.StepAsync(_api, reviewer, "return", new { reason = "" });
            Assert.Equal(400, noReason.Code);
            Assert.Equal("GRADE_REASON_REQUIRED", noReason.ErrorCode);

            var returned = await scenario.StepAsync(_api, reviewer, "return", new { reason = "Quiz scores look wrong" });
            Assert.Equal(200, returned.Code);
            Assert.Equal("Draft", returned.Prop("status").GetString());

            var sheet = await scenario.SheetAsync(_api, admin);
            Assert.All(StatusesOf(sheet), status => Assert.Equal("Draft", status));

            // The instructor can correct and submit again.
            Assert.Equal(200, (await scenario.EnterAsync(_api, admin, scenario.Students[0].Id, quiz: 18m)).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);
        }

        [Fact]
        public async Task A_locked_grade_cannot_be_changed()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);
            var reviewer = await GradingScenario.ReviewerAsync(_api);
            var approver = await GradingScenario.ApproverAsync(_api);

            await scenario.EnterAllAsync(_api, admin);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, reviewer, "review")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, approver, "approve")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "publish")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "lock")).Code);

            var changed = await scenario.EnterAsync(_api, admin, scenario.Students[0].Id, quiz: 1m, final: 1m);

            Assert.Equal(422, changed.Code);
            Assert.Equal("GRADE_LOCKED", changed.ErrorCode);
        }

        [Fact]
        public async Task Scores_are_checked_against_the_scheme_and_the_roster()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);
            var student = scenario.Students[0].Id;

            var tooHigh = await scenario.EnterAsync(_api, admin, student, quiz: 25m);
            Assert.Equal(400, tooHigh.Code);
            Assert.Equal("GRADE_SCORE_INVALID", tooHigh.ErrorCode);

            var unknown = await Client.PutAsync($"/api/v1/grades/sections/{scenario.SectionId}/students/{student}", new { scores = new[] { new { componentId = Guid.NewGuid(), score = 5m } } }, admin);
            Assert.Equal(400, unknown.Code);
            Assert.Equal("GRADE_COMPONENT_NOT_IN_SCHEME", unknown.ErrorCode);

            var stranger = await Client.PutAsync($"/api/v1/grades/sections/{scenario.SectionId}/students/{Guid.NewGuid()}", new { scores = new[] { new { componentId = scenario.QuizId, score = 5m } } }, admin);
            Assert.Equal(422, stranger.Code);
            Assert.Equal("GRADE_STUDENT_NOT_ON_ROSTER", stranger.ErrorCode);
        }

        [Fact]
        public async Task Grades_cannot_be_entered_before_the_assessment_is_published()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1, publishAssessment: false);

            var entered = await scenario.EnterAsync(_api, admin, scenario.Students[0].Id);

            Assert.Equal(422, entered.Code);
            Assert.Equal("GRADE_ASSESSMENT_NOT_READY", entered.ErrorCode);
        }

        [Fact]
        public async Task A_published_assessment_with_grades_cannot_be_changed()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);

            Assert.Equal(200, (await scenario.EnterAsync(_api, admin, scenario.Students[0].Id)).Code);

            var removed = await Client.DeleteAsync($"/api/v1/assessments/{scenario.AssessmentId}/components/{scenario.QuizId}", admin);

            Assert.Equal(422, removed.Code);
            Assert.Contains(removed.ErrorCode, new[] { "ASSESSMENT_HAS_GRADES", "ASSESSMENT_INVALID_STATE" });
        }

        [Fact]
        public async Task A_student_sees_their_own_published_grade_and_nobody_elses()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 2, createAccounts: true);
            var reviewer = await GradingScenario.ReviewerAsync(_api);
            var approver = await GradingScenario.ApproverAsync(_api);

            await scenario.EnterAllAsync(_api, admin);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, reviewer, "review")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, approver, "approve")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "publish")).Code);

            var first = scenario.Students[0];
            var second = scenario.Students[1];
            var token = await _api.LoginTokenAsync(first.Number, TestApi.DefaultPassword);

            var own = await Client.GetAsync($"/api/v1/grades/students/{first.Id}", token);
            Assert.Equal(200, own.Code);
            Assert.Equal(1, own.Body.GetArrayLength());
            Assert.Equal("Published", own.Body[0].GetProperty("status").GetString());

            var other = await Client.GetAsync($"/api/v1/grades/students/{second.Id}", token);
            Assert.Equal(403, other.Code);

            // A student cannot enter or publish grades.
            var enter = await scenario.EnterAsync(_api, token, first.Id, quiz: 20m, final: 100m);
            Assert.Equal(403, enter.Code);

            var publish = await scenario.StepAsync(_api, token, "publish");
            Assert.Equal(403, publish.Code);
        }

        [Fact]
        public async Task An_instructor_enters_grades_only_for_the_section_they_are_assigned_to()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);
            var other = await GradingScenario.BuildAsync(_api, admin, students: 1);

            var instructor = await RoleUsers.TokenAsync(_api, "Teacher", [Permissions.Grades.Read, Permissions.Grades.Enter], ScopeType.Section, scenario.SectionId);

            var own = await scenario.EnterAsync(_api, instructor, scenario.Students[0].Id);
            Assert.Equal(200, own.Code);

            var foreign = await other.EnterAsync(_api, instructor, other.Students[0].Id);
            Assert.Equal(403, foreign.Code);

            // Entering grades is not publishing them.
            var publish = await scenario.StepAsync(_api, instructor, "publish");
            Assert.Equal(403, publish.Code);
        }
    }
}
