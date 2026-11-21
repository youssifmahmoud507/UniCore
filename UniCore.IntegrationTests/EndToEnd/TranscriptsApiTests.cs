using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // GPA, academic history and official transcripts of a student whose grades went through the whole chain.
    [Collection(ApiCollection.Name)]
    public sealed class TranscriptsApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        // Quiz 20/20 and Final 50/100 give 70 percent. The grades are entered, submitted, reviewed, approved and published.
        private async Task<GradingScenario> PublishedAsync(string admin, int students = 2, bool createAccounts = true)
        {
            var scenario = await GradingScenario.BuildAsync(_api, admin, students, createAccounts: createAccounts);
            var reviewer = await GradingScenario.ReviewerAsync(_api, scenario.Setup.DepartmentId);
            var approver = await GradingScenario.ApproverAsync(_api, scenario.Setup.DepartmentId);

            await scenario.EnterAllAsync(_api, admin);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, reviewer, "review")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, approver, "approve")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "publish")).Code);

            return scenario;
        }

        [Fact]
        public async Task The_gpa_counts_the_published_grades()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await PublishedAsync(admin, students: 1, createAccounts: false);
            var student = scenario.Students[0].Id;

            var gpa = await Client.GetAsync($"/api/v1/transcripts/students/{student}/gpa", admin);

            Assert.Equal(200, gpa.Code);
            var cumulative = gpa.Prop("cumulative");
            Assert.Equal(3, cumulative.GetProperty("attemptedCredits").GetInt32());
            Assert.True(cumulative.GetProperty("gpa").GetDecimal() > 0m);

            var semester = await Client.GetAsync($"/api/v1/transcripts/students/{student}/semesters/{scenario.Setup.Calendar.SemesterId}/gpa", admin);
            Assert.Equal(200, semester.Code);
            Assert.Equal(3, semester.Prop("semester").GetProperty("attemptedCredits").GetInt32());

            var other = await Client.GetAsync($"/api/v1/transcripts/students/{student}/semesters/{Guid.NewGuid()}/gpa", admin);
            Assert.Equal(422, other.Code);
            Assert.Equal("TRANSCRIPT_NO_GRADES", other.ErrorCode);
        }

        [Fact]
        public async Task The_history_lists_each_semester_with_its_courses()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await PublishedAsync(admin, students: 1, createAccounts: false);
            var student = scenario.Students[0];

            var history = await Client.GetAsync($"/api/v1/transcripts/students/{student.Id}/history", admin);

            Assert.Equal(200, history.Code);
            Assert.Equal(student.Number, history.Prop("studentNumber").GetString());

            var semesters = history.Prop("semesters");
            Assert.Equal(1, semesters.GetArrayLength());

            var courses = semesters[0].GetProperty("courses");
            Assert.Equal(1, courses.GetArrayLength());
            Assert.Equal(70m, courses[0].GetProperty("percentage").GetDecimal());
            Assert.Equal(3, courses[0].GetProperty("credits").GetInt32());
            Assert.True(courses[0].GetProperty("isCounted").GetBoolean());
        }

        [Fact]
        public async Task The_unofficial_transcript_is_calculated_now_and_has_no_verification_code()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await PublishedAsync(admin, students: 1, createAccounts: false);

            var transcript = await Client.GetAsync($"/api/v1/transcripts/students/{scenario.Students[0].Id}", admin);

            Assert.Equal(200, transcript.Code);
            Assert.False(transcript.Prop("isOfficial").GetBoolean());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, transcript.Prop("snapshotId").ValueKind);
            Assert.Equal(System.Text.Json.JsonValueKind.Null, transcript.Prop("verificationCode").ValueKind);

            var snapshots = await Client.GetAsync($"/api/v1/transcripts/students/{scenario.Students[0].Id}/snapshots", admin);
            Assert.Equal(200, snapshots.Code);
            Assert.Equal(0, snapshots.Body.GetArrayLength());
        }

        [Fact]
        public async Task An_official_transcript_is_issued_stored_and_read_back_with_its_code()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await PublishedAsync(admin, students: 1, createAccounts: false);
            var student = scenario.Students[0].Id;

            var issued = await Client.PostAsync($"/api/v1/transcripts/students/{student}/snapshots", null, admin);

            Assert.Equal(200, issued.Code);
            Assert.True(issued.Prop("isOfficial").GetBoolean());
            var code = issued.Prop("verificationCode").GetString();
            Assert.False(string.IsNullOrWhiteSpace(code));
            var snapshotId = issued.GuidOf("snapshotId");

            var second = await Client.PostAsync($"/api/v1/transcripts/students/{student}/snapshots", null, admin);
            Assert.Equal(200, second.Code);
            Assert.NotEqual(code, second.Prop("verificationCode").GetString());

            var list = await Client.GetAsync($"/api/v1/transcripts/students/{student}/snapshots", admin);
            Assert.Equal(200, list.Code);
            Assert.Equal(2, list.Body.GetArrayLength());

            var read = await Client.GetAsync($"/api/v1/transcripts/snapshots/{snapshotId}", admin);
            Assert.Equal(200, read.Code);
            Assert.True(read.Prop("isOfficial").GetBoolean());
            Assert.Equal(code, read.Prop("verificationCode").GetString());
            Assert.Equal(1, read.Prop("semesters").GetArrayLength());
            Assert.Equal(70m, read.Prop("semesters")[0].GetProperty("courses")[0].GetProperty("percentage").GetDecimal());
        }

        [Fact]
        public async Task A_student_without_published_grades_gets_no_official_transcript()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1);

            var issued = await Client.PostAsync($"/api/v1/transcripts/students/{scenario.Students[0].Id}/snapshots", null, admin);

            Assert.Equal(422, issued.Code);
            Assert.Equal("TRANSCRIPT_NO_GRADES", issued.ErrorCode);
        }

        [Fact]
        public async Task A_student_reads_their_own_transcript_but_cannot_issue_one_or_read_anyone_elses()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await PublishedAsync(admin, students: 2);
            var first = scenario.Students[0];
            var second = scenario.Students[1];
            var token = await _api.LoginTokenAsync(first.Number, TestApi.DefaultPassword);

            var own = await Client.GetAsync($"/api/v1/transcripts/students/{first.Id}/gpa", token);
            Assert.Equal(200, own.Code);

            var history = await Client.GetAsync($"/api/v1/transcripts/students/{first.Id}/history", token);
            Assert.Equal(200, history.Code);

            var theirs = await Client.GetAsync($"/api/v1/transcripts/students/{second.Id}/gpa", token);
            Assert.Equal(403, theirs.Code);

            var theirHistory = await Client.GetAsync($"/api/v1/transcripts/students/{second.Id}/history", token);
            Assert.Equal(403, theirHistory.Code);

            // Issuing an official transcript is the registrar's job.
            var issue = await Client.PostAsync($"/api/v1/transcripts/students/{first.Id}/snapshots", null, token);
            Assert.Equal(403, issue.Code);

            var issued = await Client.PostAsync($"/api/v1/transcripts/students/{second.Id}/snapshots", null, admin);
            Assert.Equal(200, issued.Code);

            var snapshot = await Client.GetAsync($"/api/v1/transcripts/snapshots/{issued.GuidOf("snapshotId")}", token);
            Assert.Equal(403, snapshot.Code);
        }

        [Fact]
        public async Task An_unknown_student_or_transcript_is_not_found()
        {
            var admin = await _api.AdminTokenAsync();

            var student = await Client.GetAsync($"/api/v1/transcripts/students/{Guid.NewGuid()}/gpa", admin);
            Assert.Equal(404, student.Code);
            Assert.Equal("TRANSCRIPT_STUDENT_NOT_FOUND", student.ErrorCode);

            var snapshot = await Client.GetAsync($"/api/v1/transcripts/snapshots/{Guid.NewGuid()}", admin);
            Assert.Equal(404, snapshot.Code);
            Assert.Equal("TRANSCRIPT_NOT_FOUND", snapshot.ErrorCode);
        }
    }
}
