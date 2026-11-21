using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // Submit, cancel, drop and withdraw, and who may touch a registration. The seeding comes from AcademicScenario.
    [Collection(ApiCollection.Name)]
    public sealed class RegistrationLifecycleApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private sealed record Taken(Guid RegistrationId, Guid EnrollmentId);

        private async Task<ApiResponse> RegisterRawAsync(string token, Guid student, AcademicScenario.Setup setup, int course = 0)
            => await Client.PostAsync("/api/v1/registrations", new { studentId = student, academicSemesterId = setup.Calendar.SemesterId, courseSectionId = setup.Courses[course].SectionId }, token, TestApi.NewIdempotencyKey());

        private async Task<Taken> RegisterAsync(string token, Guid student, AcademicScenario.Setup setup, int course = 0)
        {
            var response = await RegisterRawAsync(token, student, setup, course);
            Assert.Equal(201, response.Code);

            return new Taken(response.Prop("registration").GetProperty("id").GetGuid(), response.Prop("enrollment").GetProperty("id").GetGuid());
        }

        private Task<ApiResponse> DropAsync(string token, Guid registration, params Guid[] enrollments)
            => Client.PostAsync($"/api/v1/registrations/{registration}/drop", new { enrollmentIds = enrollments }, token);

        [Fact]
        public async Task A_draft_registration_is_submitted_once()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);
            var taken = await RegisterAsync(admin, student.Id, setup);

            var submitted = await Client.PostAsync($"/api/v1/registrations/{taken.RegistrationId}/submit", null, admin);
            Assert.Equal(200, submitted.Code);
            Assert.Equal("Submitted", submitted.Prop("status").GetString());

            var again = await Client.PostAsync($"/api/v1/registrations/{taken.RegistrationId}/submit", null, admin);
            Assert.Equal(422, again.Code);
            Assert.Equal("REGISTRATION_INVALID_STATE", again.ErrorCode);
        }

        [Fact]
        public async Task A_registration_without_a_course_cannot_be_submitted()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);
            var taken = await RegisterAsync(admin, student.Id, setup);

            Assert.Equal(200, (await DropAsync(admin, taken.RegistrationId, taken.EnrollmentId)).Code);

            var submitted = await Client.PostAsync($"/api/v1/registrations/{taken.RegistrationId}/submit", null, admin);

            Assert.Equal(422, submitted.Code);
            Assert.Equal("REGISTRATION_EMPTY", submitted.ErrorCode);
        }

        [Fact]
        public async Task Dropping_a_course_frees_the_seat_and_the_student_can_take_it_back()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, capacity: 1);
            var first = await AcademicScenario.AdmitAsync(_api, admin, setup);
            var second = await AcademicScenario.AdmitAsync(_api, admin, setup);

            var taken = await RegisterAsync(admin, first.Id, setup);

            var full = await RegisterRawAsync(admin, second.Id, setup);
            Assert.Equal(422, full.Code);
            Assert.Equal("COURSE_FULL", full.ErrorCode);

            var dropped = await DropAsync(admin, taken.RegistrationId, taken.EnrollmentId);
            Assert.Equal(200, dropped.Code);
            Assert.Equal(0, dropped.Prop("registeredCredits").GetInt32());
            Assert.Equal("Dropped", dropped.Prop("enrollments")[0].GetProperty("status").GetString());

            // The freed seat goes to the other student.
            Assert.Equal(201, (await RegisterRawAsync(admin, second.Id, setup)).Code);

            // The first student has no seat to come back to now.
            var back = await RegisterRawAsync(admin, first.Id, setup);
            Assert.Equal(422, back.Code);
            Assert.Equal("COURSE_FULL", back.ErrorCode);
        }

        [Fact]
        public async Task A_dropped_course_can_be_registered_again_on_the_same_registration()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);
            var taken = await RegisterAsync(admin, student.Id, setup);

            Assert.Equal(200, (await DropAsync(admin, taken.RegistrationId, taken.EnrollmentId)).Code);

            var again = await RegisterRawAsync(admin, student.Id, setup);

            Assert.Equal(201, again.Code);
            Assert.Equal(taken.RegistrationId, again.Prop("registration").GetProperty("id").GetGuid());
            Assert.Equal(3, again.Prop("registration").GetProperty("registeredCredits").GetInt32());
        }

        [Fact]
        public async Task Dropping_twice_changes_nothing_and_an_unknown_enrollment_is_not_found()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);
            var taken = await RegisterAsync(admin, student.Id, setup);

            Assert.Equal(200, (await DropAsync(admin, taken.RegistrationId, taken.EnrollmentId)).Code);
            Assert.Equal(200, (await DropAsync(admin, taken.RegistrationId, taken.EnrollmentId)).Code);

            var unknown = await DropAsync(admin, taken.RegistrationId, Guid.NewGuid());
            Assert.Equal(404, unknown.Code);
            Assert.Equal("ENROLLMENT_NOT_FOUND", unknown.ErrorCode);

            var none = await DropAsync(admin, taken.RegistrationId);
            Assert.Equal(400, none.Code);
            Assert.Equal("REGISTRATION_ENROLLMENTS_REQUIRED", none.ErrorCode);
        }

        [Fact]
        public async Task The_same_section_cannot_be_taken_twice()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);

            await RegisterAsync(admin, student.Id, setup);

            var twice = await RegisterRawAsync(admin, student.Id, setup);

            Assert.Equal(409, twice.Code);
            Assert.Equal("ALREADY_ENROLLED", twice.ErrorCode);
        }

        [Fact]
        public async Task Cancelling_a_registration_frees_every_seat_and_closes_it()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, capacity: 1);
            var first = await AcademicScenario.AdmitAsync(_api, admin, setup);
            var second = await AcademicScenario.AdmitAsync(_api, admin, setup);
            var taken = await RegisterAsync(admin, first.Id, setup);

            var cancelled = await Client.PostAsync($"/api/v1/registrations/{taken.RegistrationId}/cancel", null, admin);
            Assert.Equal(200, cancelled.Code);
            Assert.Equal("Cancelled", cancelled.Prop("status").GetString());
            Assert.Equal(0, cancelled.Prop("registeredCredits").GetInt32());

            Assert.Equal(201, (await RegisterRawAsync(admin, second.Id, setup)).Code);

            var submit = await Client.PostAsync($"/api/v1/registrations/{taken.RegistrationId}/submit", null, admin);
            Assert.Equal(422, submit.Code);
            Assert.Equal("REGISTRATION_INVALID_STATE", submit.ErrorCode);

            var cancelAgain = await Client.PostAsync($"/api/v1/registrations/{taken.RegistrationId}/cancel", null, admin);
            Assert.Equal(422, cancelAgain.Code);
            Assert.Equal("REGISTRATION_INVALID_STATE", cancelAgain.ErrorCode);
        }

        [Fact]
        public async Task A_student_cancels_their_own_registration_while_registration_is_open()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var token = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);
            var taken = await RegisterAsync(token, student.Id, setup);

            var submitted = await Client.PostAsync($"/api/v1/registrations/{taken.RegistrationId}/submit", null, token);
            Assert.Equal(200, submitted.Code);

            var cancelled = await Client.PostAsync($"/api/v1/registrations/{taken.RegistrationId}/cancel", null, token);
            Assert.Equal(200, cancelled.Code);
            Assert.Equal("Cancelled", cancelled.Prop("status").GetString());
        }

        [Fact]
        public async Task Withdrawing_is_refused_while_add_and_drop_is_still_open()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);
            var taken = await RegisterAsync(admin, student.Id, setup);

            var withdrawn = await Client.PostAsync($"/api/v1/registrations/{taken.RegistrationId}/withdraw", new { enrollmentId = taken.EnrollmentId }, admin);

            Assert.Equal(422, withdrawn.Code);
            Assert.Equal("WITHDRAWAL_DEADLINE_PASSED", withdrawn.ErrorCode);
        }

        [Fact]
        public async Task A_student_cannot_read_or_change_another_students_registration()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, courseCount: 2);
            var owner = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var stranger = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var ownerToken = await _api.LoginTokenAsync(owner.Number, TestApi.DefaultPassword);
            var strangerToken = await _api.LoginTokenAsync(stranger.Number, TestApi.DefaultPassword);
            var taken = await RegisterAsync(ownerToken, owner.Id, setup);

            var read = await Client.GetAsync($"/api/v1/registrations/{taken.RegistrationId}", strangerToken);
            Assert.Equal(403, read.Code);
            Assert.Equal("FORBIDDEN_NOT_OWNER", read.ErrorCode);

            var submit = await Client.PostAsync($"/api/v1/registrations/{taken.RegistrationId}/submit", null, strangerToken);
            Assert.Equal(403, submit.Code);

            var cancel = await Client.PostAsync($"/api/v1/registrations/{taken.RegistrationId}/cancel", null, strangerToken);
            Assert.Equal(403, cancel.Code);

            var drop = await DropAsync(strangerToken, taken.RegistrationId, taken.EnrollmentId);
            Assert.Equal(403, drop.Code);

            // Nor can the stranger register on the owner's behalf.
            var onBehalf = await RegisterRawAsync(strangerToken, owner.Id, setup, course: 1);
            Assert.Equal(403, onBehalf.Code);
            Assert.Equal("FORBIDDEN_NOT_OWNER", onBehalf.ErrorCode);

            // Nothing changed for the owner.
            var mine = await Client.GetAsync($"/api/v1/registrations/{taken.RegistrationId}", ownerToken);
            Assert.Equal(200, mine.Code);
            Assert.Equal("Draft", mine.Prop("status").GetString());
            Assert.Equal(3, mine.Prop("registeredCredits").GetInt32());
        }

        [Fact]
        public async Task A_registration_that_does_not_exist_is_not_found()
        {
            var admin = await _api.AdminTokenAsync();

            var response = await Client.GetAsync($"/api/v1/registrations/{Guid.NewGuid()}", admin);

            Assert.Equal(404, response.Code);
            Assert.Equal("REGISTRATION_NOT_FOUND", response.ErrorCode);
        }
    }
}
