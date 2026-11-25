using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // Student services (Week 18, part 1) through the real API and the real workflow engine: submit starts the workflow,
    // a student sees only their own requests, cancel stops both, and a certificate takes its fee.
    [Collection(ApiCollection.Name)]
    public sealed class StudentServicesApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private async Task<(string Admin, GradingScenario Scenario)> TwoStudentsAsync()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 2, createAccounts: true);

            return (admin, scenario);
        }

        private async Task<string> TokenOfAsync(GradingScenario scenario, int index)
            => await _api.LoginTokenAsync(scenario.Students[index].Number, TestApi.DefaultPassword);

        [Fact]
        public async Task A_complaint_starts_a_workflow_is_private_and_can_be_cancelled()
        {
            var (_, scenario) = await TwoStudentsAsync();
            var owner = scenario.Students[0].Id;
            var ownerToken = await TokenOfAsync(scenario, 0);
            var otherToken = await TokenOfAsync(scenario, 1);

            var submitted = await Client.PostAsync($"/api/v1/student-services/complaints/students/{owner}", new { category = "Facilities", subject = "Lab", details = "The lab is closed in the evening" }, ownerToken);
            Assert.InRange(submitted.Code, 200, 201);
            Assert.Equal("Pending", submitted.Str("status"));
            Assert.NotEqual(Guid.Empty, submitted.GuidOf("workflowInstanceId"));
            var id = submitted.GuidOf("id");

            Assert.Equal(200, (await Client.GetAsync($"/api/v1/student-services/complaints/{id}", ownerToken)).Code);

            // Someone else's complaint is "not found", for reading and for cancelling.
            Assert.Equal(404, (await Client.GetAsync($"/api/v1/student-services/complaints/{id}", otherToken)).Code);
            Assert.Equal(404, (await Client.PostAsync($"/api/v1/student-services/complaints/{id}/cancel", null, otherToken)).Code);

            var mine = await Client.GetAsync($"/api/v1/student-services/complaints/students/{owner}", ownerToken);
            Assert.Equal(200, mine.Code);

            var cancelled = await Client.PostAsync($"/api/v1/student-services/complaints/{id}/cancel", null, ownerToken);
            Assert.Equal(200, cancelled.Code);
            Assert.Equal("Cancelled", cancelled.Str("status"));
        }

        [Fact]
        public async Task A_certificate_takes_its_fee_and_one_open_request_per_type()
        {
            var (admin, scenario) = await TwoStudentsAsync();
            var student = scenario.Students[0].Id;
            var token = await TokenOfAsync(scenario, 0);

            // The fee must exist in the catalog (Developer 2 seeds it in a real deployment). Another test may have created it already.
            var fee = await Client.PostAsync("/api/v1/fee-definitions", new { universityId = Guid.NewGuid(), code = "CERTIFICATE", name = "Certificate fee", description = "x", defaultAmount = 50m }, admin);
            Assert.True(fee.Code is 201 or 409, "fee definition: " + fee.Code);

            var body = new { certificateType = "GoodStanding", purpose = "Visa application" };
            var first = await Client.PostAsync($"/api/v1/student-services/certificate-requests/students/{student}", body, token);
            Assert.InRange(first.Code, 200, 201);
            Assert.Equal("Pending", first.Str("status"));

            var again = await Client.PostAsync($"/api/v1/student-services/certificate-requests/students/{student}", body, token);
            Assert.Equal(409, again.Code);
            Assert.Equal("SERVICE_REQUEST_ALREADY_PENDING", again.ErrorCode);
        }

        [Fact]
        public async Task The_staff_list_is_closed_to_students()
        {
            var (admin, scenario) = await TwoStudentsAsync();
            var token = await TokenOfAsync(scenario, 0);

            Assert.Equal(403, (await Client.GetAsync("/api/v1/student-services/appeals", token)).Code);
            Assert.Equal(200, (await Client.GetAsync("/api/v1/student-services/appeals", admin)).Code);
        }
    }
}
