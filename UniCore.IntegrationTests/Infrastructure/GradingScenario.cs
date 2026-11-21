using UniCore.Domain.Modules.Identity;

namespace UniCore.IntegrationTests.Infrastructure
{
    // One section with registered students and a published assessment (Quiz 40 out of 20, Final 60 out of 100),
    // ready for grade entry. The semester windows come from AcademicScenario.EnsureCalendarAsync.
    public sealed class GradingScenario
    {
        public required AcademicScenario.Setup Setup { get; init; }

        public required Guid SectionId { get; init; }

        public required Guid AssessmentId { get; init; }

        public required Guid QuizId { get; init; }

        public required Guid FinalId { get; init; }

        public required IReadOnlyList<AcademicScenario.AdmittedStudent> Students { get; init; }

        public static async Task<GradingScenario> BuildAsync(TestApi api, string admin, int students = 3, bool publishAssessment = true, bool createAccounts = false)
        {
            var client = api.Client;
            var setup = await AcademicScenario.BuildAsync(api, admin, courseCount: 1, credits: 3, capacity: 30);
            var sectionId = setup.Courses[0].SectionId;

            var admitted = new List<AcademicScenario.AdmittedStudent>();
            for (var i = 0; i < students; i++)
            {
                var student = await AcademicScenario.AdmitAsync(api, admin, setup, createAccounts);
                var registered = await client.PostAsync("/api/v1/registrations", new { studentId = student.Id, academicSemesterId = setup.Calendar.SemesterId, courseSectionId = sectionId }, admin, TestApi.NewIdempotencyKey());
                Assert.Equal(201, registered.Code);
                admitted.Add(student);
            }

            var assessment = await client.PostAsync("/api/v1/assessments", new { courseSectionId = sectionId }, admin);
            Assert.Equal(201, assessment.Code);
            var assessmentId = assessment.GuidOf("id");

            var quiz = await client.PostAsync($"/api/v1/assessments/{assessmentId}/components", new { name = "Quiz 1", type = "Quiz", weight = 40m, maxScore = 20m, dueDate = AdmitSpec.Today.AddDays(-80) }, admin);
            Assert.Equal(200, quiz.Code);

            var final = await client.PostAsync($"/api/v1/assessments/{assessmentId}/components", new { name = "Final exam", type = "Final", weight = 60m, maxScore = 100m, dueDate = AdmitSpec.Today.AddDays(-15) }, admin);
            Assert.Equal(200, final.Code);

            var components = final.Prop("components").EnumerateArray().ToList();
            var quizId = components.Single(c => c.GetProperty("name").GetString() == "Quiz 1").GetProperty("id").GetGuid();
            var finalId = components.Single(c => c.GetProperty("name").GetString() == "Final exam").GetProperty("id").GetGuid();

            if (publishAssessment)
            {
                Assert.Equal(200, (await client.PostAsync($"/api/v1/assessments/{assessmentId}/publish", null, admin)).Code);
            }

            return new GradingScenario { Setup = setup, SectionId = sectionId, AssessmentId = assessmentId, QuizId = quizId, FinalId = finalId, Students = admitted };
        }

        // Quiz 20/20 and Final 50/100 give 40 + 30 = 70 percent.
        public Task<ApiResponse> EnterAsync(TestApi api, string token, Guid studentId, decimal quiz = 20m, decimal final = 50m)
            => api.Client.PutAsync($"/api/v1/grades/sections/{SectionId}/students/{studentId}", new { scores = new[] { new { componentId = QuizId, score = quiz }, new { componentId = FinalId, score = final } } }, token);

        public async Task EnterAllAsync(TestApi api, string token)
        {
            foreach (var student in Students)
            {
                Assert.Equal(200, (await EnterAsync(api, token, student.Id)).Code);
            }
        }

        public Task<ApiResponse> StepAsync(TestApi api, string token, string step, object? body = null)
            => api.Client.PostAsync($"/api/v1/grades/sections/{SectionId}/{step}", body, token);

        public Task<ApiResponse> SheetAsync(TestApi api, string token)
            => api.Client.GetAsync($"/api/v1/grades/sections/{SectionId}?pageSize=100", token);

        public static Task<string> ReviewerAsync(TestApi api, bool alsoApprover = false)
            => RoleUsers.TokenAsync(api, "Reviewer", alsoApprover
                ? [Permissions.Grades.Read, Permissions.Grades.Review, Permissions.Grades.Approve]
                : [Permissions.Grades.Read, Permissions.Grades.Review]);

        public static Task<string> ApproverAsync(TestApi api)
            => RoleUsers.TokenAsync(api, "Approver", [Permissions.Grades.Read, Permissions.Grades.Approve]);
    }
}
