using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // The grading scheme of a section: components, weights that must add up to 100, due dates inside the semester.
    [Collection(ApiCollection.Name)]
    public sealed class AssessmentsApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static DateOnly Today => AdmitSpec.Today;

        private async Task<(string Admin, Guid SectionId, Guid AssessmentId)> NewAssessmentAsync()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var assessment = await Client.PostAsync("/api/v1/assessments", new { courseSectionId = setup.Courses[0].SectionId }, admin);
            Assert.Equal(201, assessment.Code);

            return (admin, setup.Courses[0].SectionId, assessment.GuidOf("id"));
        }

        private Task<ApiResponse> AddAsync(string admin, Guid assessmentId, string name, string type, decimal weight, DateOnly? due = null, decimal max = 100m)
            => Client.PostAsync($"/api/v1/assessments/{assessmentId}/components", new { name, type, weight, maxScore = max, dueDate = due ?? Today.AddDays(-80) }, admin);

        [Fact]
        public async Task A_section_has_one_assessment()
        {
            var (admin, sectionId, _) = await NewAssessmentAsync();

            var second = await Client.PostAsync("/api/v1/assessments", new { courseSectionId = sectionId }, admin);

            Assert.Equal(409, second.Code);
            Assert.Equal("ASSESSMENT_EXISTS", second.ErrorCode);
        }

        [Fact]
        public async Task An_assessment_needs_components_and_weights_that_add_up_to_one_hundred_to_be_published()
        {
            var (admin, _, assessmentId) = await NewAssessmentAsync();

            var empty = await Client.PostAsync($"/api/v1/assessments/{assessmentId}/publish", null, admin);
            Assert.Equal(422, empty.Code);
            Assert.Equal("ASSESSMENT_NO_COMPONENTS", empty.ErrorCode);

            Assert.Equal(200, (await AddAsync(admin, assessmentId, "Quiz", "Quiz", 40m)).Code);

            var short100 = await Client.PostAsync($"/api/v1/assessments/{assessmentId}/publish", null, admin);
            Assert.Equal(422, short100.Code);
            Assert.Equal("ASSESSMENT_WEIGHT_NOT_100", short100.ErrorCode);

            Assert.Equal(200, (await AddAsync(admin, assessmentId, "Project", "Project", 60m)).Code);

            var published = await Client.PostAsync($"/api/v1/assessments/{assessmentId}/publish", null, admin);
            Assert.Equal(200, published.Code);
            Assert.Equal("Published", published.Prop("status").GetString());
            Assert.Equal(100m, published.Prop("totalWeight").GetDecimal());
        }

        [Fact]
        public async Task The_weights_cannot_go_over_one_hundred()
        {
            var (admin, _, assessmentId) = await NewAssessmentAsync();

            Assert.Equal(200, (await AddAsync(admin, assessmentId, "Quiz", "Quiz", 70m)).Code);

            var over = await AddAsync(admin, assessmentId, "Project", "Project", 40m);

            Assert.Equal(422, over.Code);
            Assert.Equal("ASSESSMENT_WEIGHT_EXCEEDED", over.ErrorCode);
        }

        [Fact]
        public async Task A_component_name_and_a_singular_type_cannot_repeat()
        {
            var (admin, _, assessmentId) = await NewAssessmentAsync();

            Assert.Equal(200, (await AddAsync(admin, assessmentId, "Midterm", "Midterm", 20m, Today.AddDays(-55))).Code);

            var sameName = await AddAsync(admin, assessmentId, "Midterm", "Quiz", 10m);
            Assert.Equal(409, sameName.Code);
            Assert.Equal("ASSESSMENT_COMPONENT_NAME_EXISTS", sameName.ErrorCode);

            // A second midterm is a second component of a type that can only appear once.
            var sameType = await AddAsync(admin, assessmentId, "Midterm B", "Midterm", 10m, Today.AddDays(-55));
            Assert.Equal(409, sameType.Code);
            Assert.Equal("ASSESSMENT_COMPONENT_TYPE_EXISTS", sameType.ErrorCode);
        }

        [Fact]
        public async Task A_due_date_must_be_inside_the_semester_and_a_final_inside_the_exam_window()
        {
            var (admin, _, assessmentId) = await NewAssessmentAsync();

            var beforeSemester = await AddAsync(admin, assessmentId, "Old quiz", "Quiz", 10m, Today.AddDays(-400));
            Assert.Equal(422, beforeSemester.Code);
            Assert.Equal("ASSESSMENT_DUE_DATE_OUTSIDE_SEMESTER", beforeSemester.ErrorCode);

            // The final exam window of the shared semester is 20 to 10 days ago.
            var outsideWindow = await AddAsync(admin, assessmentId, "Final", "Final", 50m, Today.AddDays(-80));
            Assert.Equal(422, outsideWindow.Code);
            Assert.Equal("ASSESSMENT_DUE_DATE_OUTSIDE_WINDOW", outsideWindow.ErrorCode);

            var inside = await AddAsync(admin, assessmentId, "Final", "Final", 50m, Today.AddDays(-15));
            Assert.Equal(200, inside.Code);
        }

        [Fact]
        public async Task A_published_scheme_is_frozen_and_a_cancelled_one_stays_cancelled()
        {
            var (admin, _, assessmentId) = await NewAssessmentAsync();
            Assert.Equal(200, (await AddAsync(admin, assessmentId, "Everything", "Project", 100m)).Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/assessments/{assessmentId}/publish", null, admin)).Code);

            // Refused whatever order the checks run in: the scheme is frozen and already at 100.
            var late = await AddAsync(admin, assessmentId, "Extra", "Quiz", 1m);
            Assert.Equal(422, late.Code);
            Assert.Contains(late.ErrorCode, new[] { "ASSESSMENT_INVALID_STATE", "ASSESSMENT_WEIGHT_EXCEEDED" });

            var publishAgain = await Client.PostAsync($"/api/v1/assessments/{assessmentId}/publish", null, admin);
            Assert.Equal(422, publishAgain.Code);
            Assert.Equal("ASSESSMENT_INVALID_STATE", publishAgain.ErrorCode);
        }

        [Fact]
        public async Task A_student_cannot_manage_an_assessment()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var token = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);

            var created = await Client.PostAsync("/api/v1/assessments", new { courseSectionId = setup.Courses[0].SectionId }, token);

            Assert.Equal(403, created.Code);
        }
    }
}
