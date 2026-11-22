using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // Rules that depend on the calendar or on the timetable: the registration window, withdrawing, and schedule conflicts.
    // The window tests move the windows of the shared semester for their own duration and restore them afterwards
    // (AcademicScenario.WithWindowsAsync); they are safe because all API tests run one after the other.
    [Collection(ApiCollection.Name)]
    public sealed class RegistrationRulesApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;
        private readonly ApiFixture _fixture = fixture;

        private ApiClient Client => _api.Client;

        private static DateOnly Today => AdmitSpec.Today;

        private static string Short(int length = 8) => Guid.NewGuid().ToString("N")[..length].ToUpperInvariant();

        private Task<ApiResponse> RegisterAsync(string token, Guid student, AcademicScenario.Setup setup, int course = 0, string? overrideReason = null)
            => Client.PostAsync("/api/v1/registrations", new { studentId = student, academicSemesterId = setup.Calendar.SemesterId, courseSectionId = setup.Courses[course].SectionId, overrideReason }, token, TestApi.NewIdempotencyKey());

        [Fact]
        public async Task Registration_is_refused_outside_its_window_and_only_staff_can_override_it_with_a_reason()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var token = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);

            // The registration window opens in ten days.
            var windows = AcademicScenario.Windows(registrationStart: Today.AddDays(10), registrationEnd: Today.AddDays(100));

            await AcademicScenario.WithWindowsAsync(_api, admin, windows, async () =>
            {
                var closed = await RegisterAsync(token, student.Id, setup);
                Assert.Equal(422, closed.Code);
                Assert.Equal("REGISTRATION_CLOSED", closed.ErrorCode);

                // A student cannot skip the window, even with a reason.
                var byStudent = await RegisterAsync(token, student.Id, setup, overrideReason: "Please let me in");
                Assert.Equal(403, byStudent.Code);
                Assert.Equal("REGISTRATION_OVERRIDE_FORBIDDEN", byStudent.ErrorCode);

                var shortReason = await RegisterAsync(admin, student.Id, setup, overrideReason: "no");
                Assert.Equal(400, shortReason.Code);
                Assert.Equal("REGISTRATION_OVERRIDE_REASON_INVALID", shortReason.ErrorCode);

                var overridden = await RegisterAsync(admin, student.Id, setup, overrideReason: "Late approval by the dean");
                Assert.Equal(201, overridden.Code);
            });
        }

        [Fact]
        public async Task A_student_withdraws_after_add_and_drop_ended_and_the_seat_goes_back()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, capacity: 1);
            var first = await AcademicScenario.AdmitAsync(_api, admin, setup);
            var second = await AcademicScenario.AdmitAsync(_api, admin, setup);

            // Add/drop ended yesterday and the withdrawal deadline is 200 days away.
            var windows = AcademicScenario.Windows(addDropEnd: Today.AddDays(-1));

            await AcademicScenario.WithWindowsAsync(_api, admin, windows, async () =>
            {
                var taken = await RegisterAsync(admin, first.Id, setup);
                Assert.Equal(201, taken.Code);

                var full = await RegisterAsync(admin, second.Id, setup);
                Assert.Equal(422, full.Code);
                Assert.Equal("COURSE_FULL", full.ErrorCode);

                var registrationId = taken.Prop("registration").GetProperty("id").GetGuid();
                var enrollmentId = taken.Prop("enrollment").GetProperty("id").GetGuid();

                var withdrawn = await Client.PostAsync($"/api/v1/registrations/{registrationId}/withdraw", new { enrollmentId }, admin);
                Assert.Equal(200, withdrawn.Code);

                // A withdrawn enrollment is final.
                var again = await Client.PostAsync($"/api/v1/registrations/{registrationId}/withdraw", new { enrollmentId }, admin);
                Assert.Equal(422, again.Code);
                Assert.Equal("ENROLLMENT_INVALID_STATE", again.ErrorCode);

                // The seat is free again.
                var seat = await RegisterAsync(admin, second.Id, setup);
                Assert.Equal(201, seat.Code);
            });
        }

        [Fact]
        public async Task A_student_cannot_take_two_sections_that_meet_at_overlapping_times()
        {
            var admin = await _api.AdminTokenAsync();

            // Three courses with three different instructors, so only the student can clash.
            var calendar = await AcademicScenario.EnsureCalendarAsync(_api, admin);
            var (departmentId, programId, programCode) = await AcademicScenario.CreateProgramAsync(_api, admin);
            var courses = new List<AcademicScenario.OfferedCourse>();
            for (var i = 0; i < 3; i++)
            {
                var instructor = await AcademicScenario.CreateInstructorAsync(_api, admin, departmentId);
                courses.Add(await AcademicScenario.CreateOfferedCourseAsync(_api, admin, departmentId, calendar.SemesterId, instructor, credits: 3, capacity: 30));
            }

            var planVersionId = await AcademicScenario.CreatePlanAsync(_api, admin, programId, courses.Select(c => c.VersionId));
            var setup = new AcademicScenario.Setup(departmentId, programId, programCode, planVersionId, calendar, courses);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);

            var schedule = await Client.PostAsync("/api/v1/schedules", new { academicSemesterId = calendar.SemesterId, name = $"Timetable {Short(8)}" }, admin);
            Assert.Equal(201, schedule.Code);
            var scheduleId = schedule.GuidOf("id");

            async Task<Guid> SlotAsync(string start, string end)
            {
                var added = await Client.PostAsync($"/api/v1/schedules/{scheduleId}/slots", new { dayOfWeek = "Monday", startTime = start, endTime = end }, admin);
                Assert.Equal(200, added.Code);

                return added.Prop("slots").EnumerateArray().Single(s => s.GetProperty("startTime").GetString()!.StartsWith(start, StringComparison.Ordinal)).GetProperty("id").GetGuid();
            }

            async Task MeetAsync(Guid sectionId, Guid slotId)
            {
                var room = await RoomSeed.RoomAsync(_fixture, _api, admin, capacity: 40);
                var meeting = await Client.PostAsync($"/api/v1/schedules/{scheduleId}/meetings", new { courseSectionId = sectionId, scheduleSlotId = slotId, roomId = room }, admin);
                Assert.Equal(201, meeting.Code);
            }

            // 09:00-11:00, 10:00-12:00 (overlaps the first) and 11:00-13:00 (only touches the first).
            await MeetAsync(courses[0].SectionId, await SlotAsync("09:00", "11:00"));
            await MeetAsync(courses[1].SectionId, await SlotAsync("10:00", "12:00"));
            await MeetAsync(courses[2].SectionId, await SlotAsync("11:00", "13:00"));

            Assert.Equal(201, (await RegisterAsync(admin, student.Id, setup, course: 0)).Code);

            var clash = await RegisterAsync(admin, student.Id, setup, course: 1);
            Assert.Equal(422, clash.Code);
            Assert.Equal("SCHEDULE_CONFLICT", clash.ErrorCode);

            var touching = await RegisterAsync(admin, student.Id, setup, course: 2);
            Assert.Equal(201, touching.Code);
        }
    }
}
