using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // Graduation and money: a financial hold on the Graduation area blocks the decision, a hold on another area does not,
    // and releasing the hold lets the same application be approved. The registration half of this flow is in
    // RegistrationApiTests (A_financial_hold_blocks_registration_until_it_is_released).
    [Collection(ApiCollection.Name)]
    public sealed class GraduationFinanceApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private Task<ApiResponse> PlaceHoldAsync(string token, Guid student, params string[] areas)
            => Client.PostAsync("/api/v1/financial-holds", new { studentId = student, areas, note = "Unpaid fees" }, token);

        private Task<ApiResponse> ApproveAsync(string token, Guid application)
            => Client.PostAsync($"/api/v1/graduation/applications/{application}/approve", new { note = "Congratulations" }, token);

        // One student with a published A+ in the program's only course, a program of 3 credits, and an account.
        private async Task<(string Admin, GradingScenario Scenario, Guid Student, string StudentToken)> EligibleStudentAsync()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1, createAccounts: true);
            var student = scenario.Students[0];
            var reviewer = await GradingScenario.ReviewerAsync(_api, scenario.Setup.DepartmentId);
            var approver = await GradingScenario.ApproverAsync(_api, scenario.Setup.DepartmentId);

            Assert.Equal(200, (await scenario.EnterAsync(_api, admin, student.Id, quiz: 20m, final: 100m)).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "submit")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, reviewer, "review")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, approver, "approve")).Code);
            Assert.Equal(200, (await scenario.StepAsync(_api, admin, "publish")).Code);

            // The scenario program asks for 140 credits; make it 3 so the one published course is the whole program.
            var updated = await Client.PutAsync($"/api/v1/academic-programs/{scenario.Setup.ProgramId}", new { name = "Test Program", degreeType = "Bachelor", durationYears = 4, totalRequiredCredits = 3 }, admin);
            Assert.Equal(200, updated.Code);

            return (admin, scenario, student.Id, await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword));
        }

        [Fact]
        public async Task A_graduation_hold_blocks_the_decision_until_it_is_released()
        {
            var (admin, _, student, studentToken) = await EligibleStudentAsync();

            var eligibility = await Client.GetAsync($"/api/v1/graduation/students/{student}/eligibility", studentToken);
            Assert.Equal(200, eligibility.Code);
            Assert.True(eligibility.Prop("isEligible").GetBoolean());

            var applied = await Client.PostAsync($"/api/v1/graduation/students/{student}/applications", null, studentToken);
            Assert.Equal(201, applied.Code);
            var application = applied.GuidOf("id");

            // A hold on another area does not matter; the Graduation one does.
            Assert.Equal(201, (await PlaceHoldAsync(admin, student, "Registration")).Code);
            var graduationHold = await PlaceHoldAsync(admin, student, "Graduation");
            Assert.Equal(201, graduationHold.Code);

            var blocked = await ApproveAsync(admin, application);
            Assert.Equal(422, blocked.Code);
            Assert.Equal("FINANCIAL_HOLD", blocked.ErrorCode);

            var still = await Client.GetAsync($"/api/v1/graduation/applications/{application}", admin);
            Assert.Equal("Submitted", still.Str("status"));
            var notGraduated = await Client.GetAsync($"/api/v1/students/{student}", admin);
            Assert.Equal("Active", notGraduated.Prop("summary").GetProperty("status").GetString());

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/financial-holds/{graduationHold.GuidOf("id")}/release", new { note = "Paid at the desk" }, admin)).Code);

            var approved = await ApproveAsync(admin, application);
            Assert.Equal(200, approved.Code);
            Assert.Equal("Approved", approved.Str("status"));
            Assert.Equal(4.0m, approved.Prop("cumulativeGpa").GetDecimal());

            var graduated = await Client.GetAsync($"/api/v1/students/{student}", admin);
            Assert.Equal("Graduated", graduated.Prop("summary").GetProperty("status").GetString());
        }

        [Fact]
        public async Task A_hold_over_every_area_also_blocks_graduation_and_a_student_cannot_decide_for_themselves()
        {
            var (admin, _, student, studentToken) = await EligibleStudentAsync();
            var applied = await Client.PostAsync($"/api/v1/graduation/students/{student}/applications", null, studentToken);
            Assert.Equal(201, applied.Code);
            var application = applied.GuidOf("id");

            // A hold over every area.
            var hold = await PlaceHoldAsync(admin, student, "Registration", "Services", "Graduation");
            Assert.Equal(201, hold.Code);

            Assert.Equal("FINANCIAL_HOLD", (await ApproveAsync(admin, application)).ErrorCode);
            Assert.Equal(403, (await ApproveAsync(studentToken, application)).Code);
        }
    }
}
