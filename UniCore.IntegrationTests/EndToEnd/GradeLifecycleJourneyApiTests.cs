using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // What a student's record shows while grades are still moving: nothing until publication, then the LAST scores that
    // were entered (a returned sheet is corrected first), and an official transcript that does not change afterwards.
    [Collection(ApiCollection.Name)]
    public sealed class GradeLifecycleJourneyApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        [Fact]
        public async Task Journey_a_returned_sheet_is_corrected_and_only_the_published_scores_reach_the_transcript()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1, createAccounts: true);
            var student = scenario.Students[0];
            var studentToken = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);
            var reviewer = await GradingScenario.ReviewerAsync(_api, scenario.Setup.DepartmentId);
            var approver = await GradingScenario.ApproverAsync(_api, scenario.Setup.DepartmentId);

            // 1. First draft: 70 percent (quiz 20/20, final 50/100). Nothing exists for the transcript yet.
            Assert.Equal(200, (await scenario.EnterAsync(_api, admin, student.Id, quiz: 20m, final: 50m)).Code);

            var noGrades = await Client.GetAsync($"/api/v1/transcripts/students/{student.Id}/gpa", admin);
            Assert.Equal(422, noGrades.Code);
            Assert.Equal("TRANSCRIPT_NO_GRADES", noGrades.ErrorCode);

            // 2. Submitted, then returned by the reviewer: still nothing published, for the student or the transcript.
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, reviewer, "return", new { reason = "The final exam score is wrong" })).Code);

            var own = await Client.GetAsync($"/api/v1/grades/students/{student.Id}", studentToken);
            Assert.Equal(200, own.Code);
            Assert.DoesNotContain(own.Body.EnumerateArray(), g => g.GetProperty("status").GetString() is "Submitted" or "Reviewed" or "Approved" or "Published" or "Locked");
            Assert.Equal(422, (await Client.PostAsync($"/api/v1/transcripts/students/{student.Id}/snapshots", null, admin)).Code);

            // 3. The instructor fixes the final to 100/100 and it goes through the whole chain.
            Assert.Equal(200, (await scenario.EnterAsync(_api, admin, student.Id, quiz: 20m, final: 100m)).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, reviewer, "review")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, approver, "approve")).Code);

            // Approved is still not visible to the transcript.
            Assert.Equal(422, (await Client.GetAsync($"/api/v1/transcripts/students/{student.Id}/gpa", admin)).Code);

            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "publish")).Code);

            // 4. The transcript has the corrected 100, not the first draft's 70.
            var history = await Client.GetAsync($"/api/v1/transcripts/students/{student.Id}/history", studentToken);
            Assert.Equal(200, history.Code);
            Assert.Equal(100m, history.Prop("semesters")[0].GetProperty("courses")[0].GetProperty("percentage").GetDecimal());

            var gpa = await Client.GetAsync($"/api/v1/transcripts/students/{student.Id}/gpa", studentToken);
            Assert.Equal(200, gpa.Code);
            var published = gpa.Prop("cumulative").GetProperty("gpa").GetDecimal();
            Assert.True(published > 0m);

            // 5. The official transcript is a snapshot: it keeps its content when the section is locked afterwards.
            var issued = await Client.PostAsync($"/api/v1/transcripts/students/{student.Id}/snapshots", null, admin);
            Assert.Equal(200, issued.Code);
            var snapshotId = issued.GuidOf("snapshotId");

            Assert.Equal(200, (await scenario.StepAsync(_api, approver, "lock")).Code);

            var locked = await scenario.EnterAsync(_api, admin, student.Id, quiz: 0m, final: 0m);
            Assert.Equal(422, locked.Code);
            Assert.Equal("GRADE_LOCKED", locked.ErrorCode);

            var snapshot = await Client.GetAsync($"/api/v1/transcripts/snapshots/{snapshotId}", studentToken);
            Assert.Equal(200, snapshot.Code);
            Assert.Equal(100m, snapshot.Prop("semesters")[0].GetProperty("courses")[0].GetProperty("percentage").GetDecimal());

            var after = await Client.GetAsync($"/api/v1/transcripts/students/{student.Id}/gpa", studentToken);
            Assert.Equal(published, after.Prop("cumulative").GetProperty("gpa").GetDecimal());
        }
    }
}
