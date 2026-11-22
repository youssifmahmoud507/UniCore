using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // The study plan decides what a student may register for. These steps check that rule across modules
    // (Curriculum -> Students -> Registrations): a course outside the plan is refused, an active plan version cannot be
    // edited, and a new draft version of the plan changes nothing for a student who is still on the old one.
    [Collection(ApiCollection.Name)]
    public sealed class StudyPlanJourneyApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private Task<ApiResponse> RegisterAsync(string token, Guid student, AcademicScenario.Setup setup, int course)
            => Client.PostAsync("/api/v1/registrations", new { studentId = student, academicSemesterId = setup.Calendar.SemesterId, courseSectionId = setup.Courses[course].SectionId }, token, TestApi.NewIdempotencyKey());

        [Fact]
        public async Task Journey_the_plan_version_a_student_is_on_decides_what_they_can_take()
        {
            var admin = await _api.AdminTokenAsync();

            // Two courses are offered, but the plan (and so the student) only contains the first.
            var calendar = await AcademicScenario.EnsureCalendarAsync(_api, admin);
            var (departmentId, programId, programCode) = await AcademicScenario.CreateProgramAsync(_api, admin);
            var courses = new List<AcademicScenario.OfferedCourse>();
            for (var i = 0; i < 2; i++)
            {
                var instructor = await AcademicScenario.CreateInstructorAsync(_api, admin, departmentId);
                courses.Add(await AcademicScenario.CreateOfferedCourseAsync(_api, admin, departmentId, calendar.SemesterId, instructor, credits: 3, capacity: 30));
            }

            var planVersionId = await AcademicScenario.CreatePlanAsync(_api, admin, programId, [courses[0].VersionId]);
            var setup = new AcademicScenario.Setup(departmentId, programId, programCode, planVersionId, calendar, courses);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);

            var outside = await RegisterAsync(admin, student.Id, setup, course: 1);
            Assert.Equal(422, outside.Code);
            Assert.Equal("COURSE_NOT_IN_STUDY_PLAN", outside.ErrorCode);

            // The active version is frozen: the missing course cannot simply be added to it.
            var frozen = await Client.PostAsync($"/api/v1/study-plan-versions/{planVersionId}/courses", new { courseVersionId = courses[1].VersionId, requirementType = "Required" }, admin);
            Assert.Equal(422, frozen.Code);
            Assert.Equal("STUDY_PLAN_VERSION_NOT_DRAFT", frozen.ErrorCode);

            // A new draft version of the same plan can hold both courses...
            var current = await Client.GetAsync($"/api/v1/study-plan-versions/{planVersionId}", admin);
            Assert.Equal(200, current.Code);
            var planId = current.Prop("version").GetProperty("studyPlanId").GetGuid();

            var draft = await Client.PostAsync($"/api/v1/study-plans/{planId}/versions", new { effectiveFrom = AdmitSpec.Today.AddDays(-10) }, admin);
            Assert.Equal(201, draft.Code);
            var draftId = draft.Prop("version").GetProperty("id").GetGuid();

            foreach (var course in courses)
            {
                Assert.Equal(200, (await Client.PostAsync($"/api/v1/study-plan-versions/{draftId}/courses", new { courseVersionId = course.VersionId, requirementType = "Required" }, admin)).Code);
            }

            // ...but while it is only a draft the student, who is still on the old version, is not affected.
            var stillOutside = await RegisterAsync(admin, student.Id, setup, course: 1);
            Assert.Equal(422, stillOutside.Code);
            Assert.Equal("COURSE_NOT_IN_STUDY_PLAN", stillOutside.ErrorCode);

            // The course that was always in the plan is still fine.
            Assert.Equal(201, (await RegisterAsync(admin, student.Id, setup, course: 0)).Code);
        }
    }
}
