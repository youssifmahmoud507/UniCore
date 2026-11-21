using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // Course versions, study plan versions and offerings: the versioning rules that registration and grading depend on.
    [Collection(ApiCollection.Name)]
    public sealed class CoursesAndCurriculumApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static string Short() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        private static DateOnly Today => AdmitSpec.Today;

        private async Task<Guid> NewCourseAsync(string admin, Guid departmentId, string? code = null)
        {
            var course = await Client.PostAsync("/api/v1/courses", new { departmentId, code = code ?? $"C{Short()}", title = "Course", description = "x", courseType = "Theory" }, admin);
            Assert.Equal(201, course.Code);

            return course.GuidOf("id");
        }

        private Task<ApiResponse> NewVersionAsync(string admin, Guid courseId, DateOnly effectiveFrom, int credits = 3, Guid? source = null)
            => Client.PostAsync($"/api/v1/courses/{courseId}/versions", new { sourceVersionId = source, title = "Course", description = "x", credits, lectureHours = 3, practicalHours = 0, effectiveFrom }, admin);

        // ---------- courses ----------

        [Fact]
        public async Task A_course_code_is_unique()
        {
            var admin = await _api.AdminTokenAsync();
            var (departmentId, _, _) = await AcademicScenario.CreateProgramAsync(_api, admin);
            var code = $"C{Short()}";

            await NewCourseAsync(admin, departmentId, code);

            var duplicate = await Client.PostAsync("/api/v1/courses", new { departmentId, code, title = "Other", description = "x", courseType = "Theory" }, admin);

            Assert.Equal(409, duplicate.Code);
            Assert.Equal("COURSE_CODE_EXISTS", duplicate.ErrorCode);
        }

        [Fact]
        public async Task Only_one_draft_version_can_exist_and_a_new_version_copies_from_its_source()
        {
            var admin = await _api.AdminTokenAsync();
            var (departmentId, _, _) = await AcademicScenario.CreateProgramAsync(_api, admin);
            var courseId = await NewCourseAsync(admin, departmentId);

            var first = await NewVersionAsync(admin, courseId, Today.AddDays(-150), credits: 4);
            Assert.Equal(201, first.Code);
            Assert.Equal(1, first.Prop("versionNumber").GetInt32());
            Assert.Equal("Draft", first.Prop("status").GetString());

            var second = await NewVersionAsync(admin, courseId, Today.AddDays(-100));
            Assert.Equal(409, second.Code);
            Assert.Equal("COURSE_DRAFT_EXISTS", second.ErrorCode);

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/course-versions/{first.GuidOf("id")}/activate", null, admin)).Code);

            // The next version starts from the active one: only the start date is given.
            var copy = await Client.PostAsync($"/api/v1/courses/{courseId}/versions", new { sourceVersionId = first.GuidOf("id"), effectiveFrom = Today.AddDays(-50) }, admin);
            Assert.Equal(201, copy.Code);
            Assert.Equal(2, copy.Prop("versionNumber").GetInt32());
            Assert.Equal(4, copy.Prop("credits").GetInt32());
        }

        [Fact]
        public async Task Activating_a_newer_version_retires_the_previous_one_the_day_before()
        {
            var admin = await _api.AdminTokenAsync();
            var (departmentId, _, _) = await AcademicScenario.CreateProgramAsync(_api, admin);
            var courseId = await NewCourseAsync(admin, departmentId);

            var first = await NewVersionAsync(admin, courseId, Today.AddDays(-150));
            var firstId = first.GuidOf("id");
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/course-versions/{firstId}/activate", null, admin)).Code);

            var newStart = Today.AddDays(-20);
            var second = await NewVersionAsync(admin, courseId, newStart, source: firstId);
            var secondId = second.GuidOf("id");

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/course-versions/{secondId}/activate", null, admin)).Code);

            var old = await Client.GetAsync($"/api/v1/course-versions/{firstId}", admin);
            Assert.Equal("Retired", old.Prop("status").GetString());
            Assert.Equal(newStart.AddDays(-1).ToString("yyyy-MM-dd"), old.Prop("effectiveTo").GetString());

            var current = await Client.GetAsync($"/api/v1/course-versions/{secondId}", admin);
            Assert.Equal("Active", current.Prop("status").GetString());
        }

        [Fact]
        public async Task A_version_cannot_start_before_the_version_it_replaces()
        {
            var admin = await _api.AdminTokenAsync();
            var (departmentId, _, _) = await AcademicScenario.CreateProgramAsync(_api, admin);
            var courseId = await NewCourseAsync(admin, departmentId);

            var first = await NewVersionAsync(admin, courseId, Today.AddDays(-50));
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/course-versions/{first.GuidOf("id")}/activate", null, admin)).Code);

            var earlier = await NewVersionAsync(admin, courseId, Today.AddDays(-80), source: first.GuidOf("id"));
            Assert.Equal(201, earlier.Code);

            var activated = await Client.PostAsync($"/api/v1/course-versions/{earlier.GuidOf("id")}/activate", null, admin);

            Assert.Equal(400, activated.Code);
            Assert.Equal("COURSE_VERSION_EFFECTIVE_DATE_INVALID", activated.ErrorCode);
        }

        [Fact]
        public async Task Only_a_draft_version_can_be_edited_and_a_course_with_an_active_version_cannot_be_deactivated()
        {
            var admin = await _api.AdminTokenAsync();
            var (departmentId, _, _) = await AcademicScenario.CreateProgramAsync(_api, admin);
            var courseId = await NewCourseAsync(admin, departmentId);

            var version = await NewVersionAsync(admin, courseId, Today.AddDays(-150), credits: 3);
            var versionId = version.GuidOf("id");

            var edited = await Client.PutAsync($"/api/v1/course-versions/{versionId}", new { title = "Course", description = "x", credits = 5, lectureHours = 3, practicalHours = 2, effectiveFrom = Today.AddDays(-150) }, admin);
            Assert.Equal(200, edited.Code);
            Assert.Equal(5, edited.Prop("credits").GetInt32());

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/course-versions/{versionId}/activate", null, admin)).Code);

            var tooLate = await Client.PutAsync($"/api/v1/course-versions/{versionId}", new { title = "Course", description = "x", credits = 6, lectureHours = 3, practicalHours = 2, effectiveFrom = Today.AddDays(-150) }, admin);
            Assert.Equal(422, tooLate.Code);
            Assert.Equal("COURSE_VERSION_NOT_DRAFT", tooLate.ErrorCode);

            var deactivated = await Client.PostAsync($"/api/v1/courses/{courseId}/deactivate", null, admin);
            Assert.Equal(422, deactivated.Code);
            Assert.Equal("COURSE_HAS_ACTIVE_VERSION", deactivated.ErrorCode);
        }

        // ---------- offerings and sections ----------

        [Fact]
        public async Task A_draft_course_version_cannot_be_offered()
        {
            var admin = await _api.AdminTokenAsync();
            var calendar = await AcademicScenario.EnsureCalendarAsync(_api, admin);
            var (departmentId, _, _) = await AcademicScenario.CreateProgramAsync(_api, admin);
            var courseId = await NewCourseAsync(admin, departmentId);
            var draft = await NewVersionAsync(admin, courseId, Today.AddDays(-150));

            var offering = await Client.PostAsync("/api/v1/course-offerings", new { courseVersionId = draft.GuidOf("id"), academicSemesterId = calendar.SemesterId, maxSections = 1 }, admin);

            Assert.Equal(422, offering.Code);
            Assert.Equal("OFFERING_COURSE_VERSION_NOT_EFFECTIVE", offering.ErrorCode);
        }

        [Fact]
        public async Task An_offering_has_one_version_per_semester_a_limited_number_of_sections_and_needs_a_section_to_open()
        {
            var admin = await _api.AdminTokenAsync();
            var calendar = await AcademicScenario.EnsureCalendarAsync(_api, admin);
            var (departmentId, _, _) = await AcademicScenario.CreateProgramAsync(_api, admin);
            var courseId = await NewCourseAsync(admin, departmentId);
            var version = await NewVersionAsync(admin, courseId, Today.AddDays(-150));
            var versionId = version.GuidOf("id");
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/course-versions/{versionId}/activate", null, admin)).Code);

            var offering = await Client.PostAsync("/api/v1/course-offerings", new { courseVersionId = versionId, academicSemesterId = calendar.SemesterId, maxSections = 1 }, admin);
            Assert.Equal(201, offering.Code);
            var offeringId = offering.GuidOf("id");

            var twice = await Client.PostAsync("/api/v1/course-offerings", new { courseVersionId = versionId, academicSemesterId = calendar.SemesterId, maxSections = 1 }, admin);
            Assert.Equal(409, twice.Code);
            Assert.Equal("OFFERING_EXISTS", twice.ErrorCode);

            var noSection = await Client.PostAsync($"/api/v1/course-offerings/{offeringId}/open", null, admin);
            Assert.Equal(422, noSection.Code);
            Assert.Equal("OFFERING_NEEDS_SECTION", noSection.ErrorCode);

            Assert.Equal(201, (await Client.PostAsync($"/api/v1/course-offerings/{offeringId}/sections", new { sectionCode = "A1", capacity = 20 }, admin)).Code);

            var tooMany = await Client.PostAsync($"/api/v1/course-offerings/{offeringId}/sections", new { sectionCode = "A2", capacity = 20 }, admin);
            Assert.Equal(422, tooMany.Code);
            Assert.Equal("OFFERING_MAX_SECTIONS_REACHED", tooMany.ErrorCode);
        }

        [Fact]
        public async Task A_section_code_is_unique_inside_its_offering()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);

            var duplicate = await Client.PostAsync($"/api/v1/course-offerings/{setup.Courses[0].OfferingId}/sections", new { sectionCode = "S1", capacity = 10 }, admin);

            Assert.Equal(409, duplicate.Code);
            Assert.Equal("SECTION_CODE_EXISTS", duplicate.ErrorCode);
        }

        // ---------- study plans ----------

        [Fact]
        public async Task A_study_plan_code_is_unique_and_an_empty_version_cannot_be_activated()
        {
            var admin = await _api.AdminTokenAsync();
            var (_, programId, _) = await AcademicScenario.CreateProgramAsync(_api, admin);
            var code = $"P{Short()}";

            var plan = await Client.PostAsync("/api/v1/study-plans", new { academicProgramId = programId, code, name = "Plan" }, admin);
            Assert.Equal(201, plan.Code);

            var duplicate = await Client.PostAsync("/api/v1/study-plans", new { academicProgramId = programId, code, name = "Plan again" }, admin);
            Assert.Equal(409, duplicate.Code);
            Assert.Equal("STUDY_PLAN_CODE_EXISTS", duplicate.ErrorCode);

            var version = await Client.PostAsync($"/api/v1/study-plans/{plan.GuidOf("id")}/versions", new { effectiveFrom = Today.AddDays(-150) }, admin);
            Assert.Equal(201, version.Code);

            var empty = await Client.PostAsync($"/api/v1/study-plan-versions/{version.Prop("version").GetProperty("id").GetGuid()}/activate", null, admin);
            Assert.Equal(422, empty.Code);
            Assert.Equal("STUDY_PLAN_VERSION_EMPTY", empty.ErrorCode);
        }

        [Fact]
        public async Task A_plan_has_one_draft_a_course_once_and_an_active_version_is_frozen()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var planVersionId = setup.PlanVersionId;
            var courseVersionId = setup.Courses[0].VersionId;

            // The version made by the scenario is active, so it no longer takes courses.
            var frozen = await Client.PostAsync($"/api/v1/study-plan-versions/{planVersionId}/courses", new { courseVersionId, requirementType = "Required" }, admin);
            Assert.Equal(422, frozen.Code);
            Assert.Equal("STUDY_PLAN_VERSION_NOT_DRAFT", frozen.ErrorCode);

            var active = await Client.GetAsync($"/api/v1/study-plan-versions/{planVersionId}", admin);
            var planId = active.Prop("version").GetProperty("studyPlanId").GetGuid();

            // A new draft starts from the active one.
            var draft = await Client.PostAsync($"/api/v1/study-plans/{planId}/versions", new { sourceVersionId = planVersionId, effectiveFrom = Today.AddDays(-20) }, admin);
            Assert.Equal(201, draft.Code);
            Assert.Equal(2, draft.Prop("version").GetProperty("versionNumber").GetInt32());
            Assert.Equal(1, draft.Prop("courses").GetArrayLength());

            var second = await Client.PostAsync($"/api/v1/study-plans/{planId}/versions", new { effectiveFrom = Today.AddDays(-10) }, admin);
            Assert.Equal(409, second.Code);
            Assert.Equal("STUDY_PLAN_DRAFT_EXISTS", second.ErrorCode);

            // The copied course is already there: adding it again is a duplicate.
            var draftId = draft.Prop("version").GetProperty("id").GetGuid();
            var again = await Client.PostAsync($"/api/v1/study-plan-versions/{draftId}/courses", new { courseVersionId, requirementType = "Required" }, admin);
            Assert.Equal(409, again.Code);
            Assert.Equal("STUDY_PLAN_COURSE_DUPLICATE", again.ErrorCode);
        }

        [Fact]
        public async Task Activating_a_newer_plan_version_retires_the_old_one()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var oldVersionId = setup.PlanVersionId;

            var old = await Client.GetAsync($"/api/v1/study-plan-versions/{oldVersionId}", admin);
            var planId = old.Prop("version").GetProperty("studyPlanId").GetGuid();

            var draft = await Client.PostAsync($"/api/v1/study-plans/{planId}/versions", new { sourceVersionId = oldVersionId, effectiveFrom = Today.AddDays(-20) }, admin);
            Assert.Equal(201, draft.Code);
            var draftId = draft.Prop("version").GetProperty("id").GetGuid();

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/study-plan-versions/{draftId}/activate", null, admin)).Code);

            var after = await Client.GetAsync($"/api/v1/study-plan-versions/{oldVersionId}", admin);
            Assert.Equal("Retired", after.Prop("version").GetProperty("status").GetString());
        }
    }
}
