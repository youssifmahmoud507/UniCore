using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class RegistrationApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private Task<ApiResponse> RegisterAsync(string token, Guid student, Guid semester, Guid section, IReadOnlyDictionary<string, string>? key = null)
            => Client.PostAsync("/api/v1/registrations", new { studentId = student, academicSemesterId = semester, courseSectionId = section }, token, key ?? TestApi.NewIdempotencyKey());

        [Fact]
        public async Task A_registrar_registers_a_student_and_takes_a_seat()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, courseCount: 1, credits: 3);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);

            var response = await RegisterAsync(admin, student.Id, setup.Calendar.SemesterId, setup.Courses[0].SectionId);

            Assert.Equal(201, response.Code);
            Assert.False(response.Prop("isReplay").GetBoolean());
            Assert.Equal(3, response.Prop("registration").GetProperty("registeredCredits").GetInt32());
            Assert.Equal(student.Id, response.Prop("registration").GetProperty("studentId").GetGuid());
        }

        [Fact]
        public async Task A_student_registers_themselves_and_reads_their_own_registration()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup, createAccount: true);
            var token = await _api.LoginTokenAsync(student.Number, TestApi.DefaultPassword);

            var response = await RegisterAsync(token, student.Id, setup.Calendar.SemesterId, setup.Courses[0].SectionId);
            Assert.Equal(201, response.Code);

            var mine = await Client.GetAsync($"/api/v1/registrations/me?semesterId={setup.Calendar.SemesterId}", token);
            Assert.Equal(200, mine.Code);
            Assert.Equal(student.Id, mine.GuidOf("studentId"));
        }

        [Fact]
        public async Task The_same_key_and_request_replays_the_first_answer_and_takes_no_second_seat()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, capacity: 1);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);
            var key = TestApi.NewIdempotencyKey();

            var first = await RegisterAsync(admin, student.Id, setup.Calendar.SemesterId, setup.Courses[0].SectionId, key);
            var again = await RegisterAsync(admin, student.Id, setup.Calendar.SemesterId, setup.Courses[0].SectionId, key);

            Assert.Equal(201, first.Code);
            Assert.Equal(200, again.Code);
            Assert.True(again.Prop("isReplay").GetBoolean());
            Assert.Equal(first.Prop("enrollment").GetProperty("id").GetGuid(), again.Prop("enrollment").GetProperty("id").GetGuid());
        }

        [Fact]
        public async Task The_same_key_for_a_different_section_is_refused()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, courseCount: 2);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);
            var key = TestApi.NewIdempotencyKey();

            Assert.Equal(201, (await RegisterAsync(admin, student.Id, setup.Calendar.SemesterId, setup.Courses[0].SectionId, key)).Code);

            var reused = await RegisterAsync(admin, student.Id, setup.Calendar.SemesterId, setup.Courses[1].SectionId, key);

            Assert.Equal(409, reused.Code);
            Assert.Equal("IDEMPOTENCY_KEY_REUSED", reused.ErrorCode);
        }

        [Fact]
        public async Task A_request_without_an_idempotency_key_is_refused()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);

            var response = await Client.PostAsync("/api/v1/registrations", new { studentId = student.Id, academicSemesterId = setup.Calendar.SemesterId, courseSectionId = setup.Courses[0].SectionId }, admin);

            Assert.Equal(400, response.Code);
            Assert.Equal("IDEMPOTENCY_KEY_INVALID", response.ErrorCode);
        }

        [Fact]
        public async Task A_financial_hold_blocks_registration_until_it_is_released()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);

            var hold = await Client.PostAsync("/api/v1/financial-holds", new { studentId = student.Id, areas = new[] { "Registration" }, note = "Unpaid fees" }, admin);
            Assert.Equal(201, hold.Code);

            var blocked = await RegisterAsync(admin, student.Id, setup.Calendar.SemesterId, setup.Courses[0].SectionId);
            Assert.Equal(422, blocked.Code);
            Assert.Equal("FINANCIAL_HOLD", blocked.ErrorCode);

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/financial-holds/{hold.GuidOf("id")}/release", new { note = "Paid at the desk" }, admin)).Code);

            var allowed = await RegisterAsync(admin, student.Id, setup.Calendar.SemesterId, setup.Courses[0].SectionId);
            Assert.Equal(201, allowed.Code);
        }

        [Fact]
        public async Task A_course_outside_the_study_plan_is_refused()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);

            // Offered in the same semester but not part of the student's plan.
            var instructor = await AcademicScenario.CreateInstructorAsync(_api, admin, setup.DepartmentId);
            var outside = await AcademicScenario.CreateOfferedCourseAsync(_api, admin, setup.DepartmentId, setup.Calendar.SemesterId, instructor, credits: 3, capacity: 10);

            var response = await RegisterAsync(admin, student.Id, setup.Calendar.SemesterId, outside.SectionId);

            Assert.Equal(422, response.Code);
            Assert.Equal("COURSE_NOT_IN_STUDY_PLAN", response.ErrorCode);
        }

        [Fact]
        public async Task The_credit_limit_is_enforced()
        {
            var admin = await _api.AdminTokenAsync();
            // 30 credits per course: the second one passes any normal limit.
            var setup = await AcademicScenario.BuildAsync(_api, admin, courseCount: 2, credits: 30);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);

            Assert.Equal(201, (await RegisterAsync(admin, student.Id, setup.Calendar.SemesterId, setup.Courses[0].SectionId)).Code);

            var second = await RegisterAsync(admin, student.Id, setup.Calendar.SemesterId, setup.Courses[1].SectionId);

            Assert.Equal(422, second.Code);
            Assert.Equal("CREDIT_LIMIT_EXCEEDED", second.ErrorCode);
        }

        [Fact]
        public async Task When_many_requests_race_for_the_last_seat_exactly_one_wins()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, capacity: 1);

            var students = new List<AcademicScenario.AdmittedStudent>();
            for (var i = 0; i < 6; i++)
            {
                students.Add(await AcademicScenario.AdmitAsync(_api, admin, setup));
            }

            var section = setup.Courses[0].SectionId;
            var keys = students.ToDictionary(s => s.Id, _ => TestApi.NewIdempotencyKey());

            var first = await Task.WhenAll(students.Select(s => RegisterAsync(admin, s.Id, setup.Calendar.SemesterId, section, keys[s.Id])));

            // A loser may see a concurrency conflict or COURSE_FULL; nothing else, and never a second seat.
            Assert.All(first.Where(r => r.Code != 201), r => Assert.Contains(r.ErrorCode, new[] { "CONCURRENCY_CONFLICT", "COURSE_FULL" }));
            Assert.True(first.Count(r => r.Code == 201) <= 1);

            // Retry every student with the SAME key: a winner replays (200), a loser settles to COURSE_FULL.
            var winners = first.Count(r => r.Code == 201);
            foreach (var student in students)
            {
                var retry = await RegisterAsync(admin, student.Id, setup.Calendar.SemesterId, section, keys[student.Id]);

                if (retry.Code == 201)
                {
                    winners++;
                    continue;
                }

                if (retry.Code == 200)
                {
                    continue;
                }

                Assert.Equal("COURSE_FULL", retry.ErrorCode);
            }

            Assert.Equal(1, winners);
        }
    }
}
