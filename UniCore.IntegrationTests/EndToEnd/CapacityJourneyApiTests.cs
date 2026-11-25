using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // Seats under pressure: churn (register, drop, register), duplicates, a study plan that does not contain the course,
    // and many students asking for the last seat at the same moment.
    [Collection(ApiCollection.Name)]
    public sealed class CapacityJourneyApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private Task<ApiResponse> RegisterAsync(string token, Guid student, AcademicScenario.Setup setup, int course = 0)
            => Client.PostAsync("/api/v1/registrations", new { studentId = student, academicSemesterId = setup.Calendar.SemesterId, courseSectionId = setup.Courses[course].SectionId }, token, TestApi.NewIdempotencyKey());

        [Fact]
        public async Task Journey_seats_follow_every_register_and_drop_in_order()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, capacity: 2);
            var students = new List<AcademicScenario.AdmittedStudent>();
            for (var i = 0; i < 4; i++)
            {
                students.Add(await AcademicScenario.AdmitAsync(_api, admin, setup));
            }

            var first = await RegisterAsync(admin, students[0].Id, setup);
            Assert.Equal(201, first.Code);
            Assert.Equal(201, (await RegisterAsync(admin, students[1].Id, setup)).Code);

            // Two seats, both taken.
            var full = await RegisterAsync(admin, students[2].Id, setup);
            Assert.Equal(422, full.Code);
            Assert.Equal("COURSE_FULL", full.ErrorCode);

            // A second attempt by someone who already holds a seat is a duplicate, not a second seat.
            var duplicate = await RegisterAsync(admin, students[0].Id, setup);
            Assert.Equal(409, duplicate.Code);
            Assert.Equal("ALREADY_ENROLLED", duplicate.ErrorCode);

            // The first student drops; exactly one seat opens.
            var registrationId = first.Prop("registration").GetProperty("id").GetGuid();
            var enrollmentId = first.Prop("enrollment").GetProperty("id").GetGuid();
            var dropped = await Client.PostAsync($"/api/v1/registrations/{registrationId}/drop", new { enrollmentIds = new[] { enrollmentId } }, admin);
            Assert.Equal(200, dropped.Code);

            Assert.Equal(201, (await RegisterAsync(admin, students[2].Id, setup)).Code);

            var fourth = await RegisterAsync(admin, students[3].Id, setup);
            Assert.Equal(422, fourth.Code);
            Assert.Equal("COURSE_FULL", fourth.ErrorCode);

            // The student who dropped has lost the seat to someone else, so coming back finds the section full.
            var back = await RegisterAsync(admin, students[0].Id, setup);
            Assert.Equal(422, back.Code);
            Assert.Equal("COURSE_FULL", back.ErrorCode);
        }

        [Fact]
        public async Task Journey_a_course_from_another_programs_plan_is_refused_even_with_free_seats()
        {
            var admin = await _api.AdminTokenAsync();
            var mine = await AcademicScenario.BuildAsync(_api, admin);
            var other = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, mine);

            var wrong = await Client.PostAsync(
                "/api/v1/registrations",
                new { studentId = student.Id, academicSemesterId = other.Calendar.SemesterId, courseSectionId = other.Courses[0].SectionId },
                admin,
                TestApi.NewIdempotencyKey());

            Assert.Equal(422, wrong.Code);
            Assert.Equal("COURSE_NOT_IN_STUDY_PLAN", wrong.ErrorCode);

            // The own course is fine, and the failed attempt left nothing behind that would block it.
            Assert.Equal(201, (await RegisterAsync(admin, student.Id, mine)).Code);
        }

        [Fact]
        public async Task Journey_five_students_race_for_the_last_seat_and_exactly_one_wins()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, capacity: 1);
            var students = new List<AcademicScenario.AdmittedStudent>();
            for (var i = 0; i < 5; i++)
            {
                students.Add(await AcademicScenario.AdmitAsync(_api, admin, setup));
            }

            var results = await Task.WhenAll(students.Select(s => RegisterAsync(admin, s.Id, setup)));

            // Nobody gets a server error, one gets the seat, and the section is not over-booked.
            Assert.DoesNotContain(results, r => r.Code >= 500);
            Assert.Equal(1, results.Count(r => r.Code == 201));

            // Whoever lost can try again later and is told the truth: the section is full.
            var loser = students[Array.FindIndex(results, r => r.Code != 201)];
            var retry = await RegisterAsync(admin, loser.Id, setup);
            Assert.Equal(422, retry.Code);
            Assert.Equal("COURSE_FULL", retry.ErrorCode);
        }
    }
}
