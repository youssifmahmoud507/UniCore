using Microsoft.Extensions.DependencyInjection;
using UniCore.Domain.Modules.University;
using UniCore.Infrastructure.Persistence;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // Exam sessions, exams, their date and time, rooms and invigilators, the conflicts between exams, and the roster.
    // The API has no endpoint for buildings and rooms, so rooms are added straight to the database.
    [Collection(ApiCollection.Name)]
    public sealed class ExaminationApiTests(ApiFixture fixture)
    {
        private static readonly SemaphoreSlim BuildingLock = new(1, 1);
        private static Guid _buildingId;

        private readonly TestApi _api = fixture.Api;
        private readonly ApiFixture _fixture = fixture;

        private ApiClient Client => _api.Client;

        private static DateOnly Today => AdmitSpec.Today;

        // The final exam window of the shared semester is 20 to 10 days ago.
        private static DateOnly ExamDay => Today.AddDays(-15);

        private static string Short(int length = 8) => Guid.NewGuid().ToString("N")[..length].ToUpperInvariant();

        private async Task<Guid> BuildingAsync(string admin)
        {
            await BuildingLock.WaitAsync();
            try
            {
                if (_buildingId != Guid.Empty)
                {
                    return _buildingId;
                }

                var university = await Client.PostAsync("/api/v1/universities", new { code = $"U{Short(7)}", name = $"Exam University {Short(8)}", description = "x", email = "u@uni.test", phone = "0100000000", website = "https://uni.test" }, admin);
                Assert.Equal(201, university.Code);

                var campus = await Client.PostAsync("/api/v1/campuses", new
                {
                    universityId = university.GuidOf("id"),
                    code = $"CP{Short(6)}",
                    name = "Exam Campus",
                    address = new { street = "1 Main Street", city = "Cairo", state = "Cairo", postalCode = "11511", country = "Egypt" },
                    timeZone = "UTC"
                }, admin);
                Assert.Equal(201, campus.Code);

                var building = Building.Create(campus.GuidOf("id"), $"B{Short(6)}", "Exam Building").Value;

                using var scope = _fixture.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Set<Building>().Add(building);
                await db.SaveChangesAsync();

                _buildingId = building.Id;
                return _buildingId;
            }
            finally
            {
                BuildingLock.Release();
            }
        }

        private async Task<Guid> RoomAsync(string admin, int capacity)
        {
            var buildingId = await BuildingAsync(admin);
            var room = Room.Create(buildingId, $"R{Short(8)}", null, capacity, RoomType.ExamHall).Value;

            using var scope = _fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<Room>().Add(room);
            await db.SaveChangesAsync();

            return room.Id;
        }

        private async Task<Guid> SessionAsync(string admin, Guid semesterId, DateOnly? start = null, DateOnly? end = null)
        {
            var session = await Client.PostAsync("/api/v1/exam-sessions", new { academicSemesterId = semesterId, name = $"Finals {Short(10)}", examType = "Final", startDate = start ?? Today.AddDays(-20), endDate = end ?? Today.AddDays(-10) }, admin);
            Assert.Equal(201, session.Code);

            return session.GuidOf("id");
        }

        private Task<ApiResponse> CreateExamAsync(string admin, Guid sectionId, Guid sessionId, string type = "Final", decimal max = 100m, int minutes = 120)
            => Client.PostAsync("/api/v1/exams", new { courseSectionId = sectionId, examSessionId = sessionId, examType = type, maxScore = max, durationMinutes = minutes }, admin);

        private Task<ApiResponse> ScheduleAsync(string admin, Guid examId, DateOnly date, string start = "09:00", string end = "12:00")
            => Client.PutAsync($"/api/v1/exams/{examId}/schedule", new { examDate = date, startTime = start, endTime = end }, admin);

        private Task<ApiResponse> AddRoomAsync(string admin, Guid examId, Guid roomId)
            => Client.PostAsync($"/api/v1/exams/{examId}/rooms", new { roomId }, admin);

        private Task<ApiResponse> AddInvigilatorAsync(string admin, Guid examId, Guid roomId, Guid instructorId, string role)
            => Client.PostAsync($"/api/v1/exams/{examId}/invigilators", new { roomId, instructorId, role }, admin);

        private async Task<Guid> NewExamAsync(string admin, Guid sectionId, Guid sessionId)
        {
            var exam = await CreateExamAsync(admin, sectionId, sessionId);
            Assert.Equal(201, exam.Code);

            return exam.GuidOf("id");
        }

        [Fact]
        public async Task A_session_stays_inside_the_exam_window_has_a_unique_name_and_closes_once()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var semester = setup.Calendar.SemesterId;

            var name = $"Finals {Short(10)}";
            var created = await Client.PostAsync("/api/v1/exam-sessions", new { academicSemesterId = semester, name, examType = "Final", startDate = Today.AddDays(-20), endDate = Today.AddDays(-10) }, admin);
            Assert.Equal(201, created.Code);
            Assert.Equal("Open", created.Prop("status").GetString());

            var sameName = await Client.PostAsync("/api/v1/exam-sessions", new { academicSemesterId = semester, name, examType = "Final", startDate = Today.AddDays(-19), endDate = Today.AddDays(-11) }, admin);
            Assert.Equal(409, sameName.Code);
            Assert.Equal("EXAM_SESSION_NAME_EXISTS", sameName.ErrorCode);

            var outside = await Client.PostAsync("/api/v1/exam-sessions", new { academicSemesterId = semester, name = $"Late {Short(10)}", examType = "Final", startDate = Today.AddDays(-5), endDate = Today.AddDays(-3) }, admin);
            Assert.Equal(422, outside.Code);
            Assert.Equal("EXAM_SESSION_OUTSIDE_WINDOW", outside.ErrorCode);

            var backwards = await Client.PostAsync("/api/v1/exam-sessions", new { academicSemesterId = semester, name = $"Back {Short(10)}", examType = "Final", startDate = Today.AddDays(-10), endDate = Today.AddDays(-20) }, admin);
            Assert.Equal(400, backwards.Code);
            Assert.Equal("EXAM_SESSION_INVALID", backwards.ErrorCode);

            var closed = await Client.PostAsync($"/api/v1/exam-sessions/{created.GuidOf("id")}/close", null, admin);
            Assert.Equal(200, closed.Code);
            Assert.Equal("Closed", closed.Prop("status").GetString());

            var again = await Client.PostAsync($"/api/v1/exam-sessions/{created.GuidOf("id")}/close", null, admin);
            Assert.Equal(422, again.Code);
            Assert.Equal("EXAM_SESSION_CLOSED", again.ErrorCode);
        }

        [Fact]
        public async Task An_exam_needs_a_matching_type_a_valid_duration_and_an_open_session_and_a_section_has_one_per_type()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var section = setup.Courses[0].SectionId;
            var session = await SessionAsync(admin, setup.Calendar.SemesterId);

            var mismatch = await CreateExamAsync(admin, section, session, type: "Midterm");
            Assert.Equal(422, mismatch.Code);
            Assert.Equal("EXAM_TYPE_MISMATCH", mismatch.ErrorCode);

            var tooShort = await CreateExamAsync(admin, section, session, minutes: 10);
            Assert.Equal(400, tooShort.Code);
            Assert.Equal("EXAM_INVALID", tooShort.ErrorCode);

            var created = await CreateExamAsync(admin, section, session);
            Assert.Equal(201, created.Code);
            Assert.Equal("Draft", created.Prop("status").GetString());

            var second = await CreateExamAsync(admin, section, session);
            Assert.Equal(409, second.Code);
            Assert.Equal("EXAM_EXISTS", second.ErrorCode);

            var otherSession = await SessionAsync(admin, setup.Calendar.SemesterId);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/exam-sessions/{otherSession}/close", null, admin)).Code);

            var inClosed = await CreateExamAsync(admin, section, otherSession);
            Assert.Equal(422, inClosed.Code);
            Assert.Equal("EXAM_SESSION_CLOSED", inClosed.ErrorCode);
        }

        [Fact]
        public async Task The_schedule_must_fit_the_duration_the_session_and_be_a_real_time_range()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin);
            var session = await SessionAsync(admin, setup.Calendar.SemesterId);
            var exam = await NewExamAsync(admin, setup.Courses[0].SectionId, session);

            var backwards = await ScheduleAsync(admin, exam, ExamDay, start: "12:00", end: "09:00");
            Assert.Equal(400, backwards.Code);
            Assert.Equal("EXAM_SCHEDULE_INVALID", backwards.ErrorCode);

            // 120 minutes of exam do not fit in one hour.
            var tooShort = await ScheduleAsync(admin, exam, ExamDay, start: "09:00", end: "10:00");
            Assert.Equal(422, tooShort.Code);
            Assert.Equal("EXAM_DURATION_EXCEEDS_SLOT", tooShort.ErrorCode);

            var outsideSession = await ScheduleAsync(admin, exam, Today.AddDays(-5));
            Assert.Equal(422, outsideSession.Code);
            Assert.Equal("EXAM_DATE_OUTSIDE_SESSION", outsideSession.ErrorCode);

            var set = await ScheduleAsync(admin, exam, ExamDay);
            Assert.Equal(200, set.Code);
            Assert.Equal(ExamDay.ToString("yyyy-MM-dd"), set.Prop("schedule").GetProperty("examDate").GetString());

            // A longer exam no longer fits the range that is already set.
            var update = await Client.PutAsync($"/api/v1/exams/{exam}", new { maxScore = 100m, durationMinutes = 240 }, admin);
            Assert.Equal(422, update.Code);
            Assert.Equal("EXAM_DURATION_EXCEEDS_SLOT", update.ErrorCode);
        }

        [Fact]
        public async Task An_exam_is_published_only_with_a_time_enough_seats_and_a_chief_in_every_room_and_then_is_frozen()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 2);
            var session = await SessionAsync(admin, scenario.Setup.Calendar.SemesterId);
            var exam = await NewExamAsync(admin, scenario.SectionId, session);
            var chief = await AcademicScenario.CreateInstructorAsync(_api, admin, scenario.Setup.DepartmentId);
            var assistant = await AcademicScenario.CreateInstructorAsync(_api, admin, scenario.Setup.DepartmentId);

            var unscheduled = await Client.PostAsync($"/api/v1/exams/{exam}/publish", null, admin);
            Assert.Equal(422, unscheduled.Code);
            Assert.Equal("EXAM_NOT_SCHEDULED", unscheduled.ErrorCode);

            Assert.Equal(200, (await ScheduleAsync(admin, exam, ExamDay)).Code);

            var noRooms = await Client.PostAsync($"/api/v1/exams/{exam}/publish", null, admin);
            Assert.Equal(422, noRooms.Code);
            Assert.Equal("EXAM_NO_ROOMS", noRooms.ErrorCode);

            // Two students are registered, one seat is not enough.
            var small = await RoomAsync(admin, capacity: 1);
            Assert.Equal(200, (await AddRoomAsync(admin, exam, small)).Code);

            var tooSmall = await Client.PostAsync($"/api/v1/exams/{exam}/publish", null, admin);
            Assert.Equal(422, tooSmall.Code);
            Assert.Equal("EXAM_CAPACITY_NOT_ENOUGH", tooSmall.ErrorCode);

            var duplicateRoom = await AddRoomAsync(admin, exam, small);
            Assert.Equal(409, duplicateRoom.Code);
            Assert.Equal("EXAM_ROOM_EXISTS", duplicateRoom.ErrorCode);

            Assert.Equal(200, (await Client.DeleteAsync($"/api/v1/exams/{exam}/rooms/{small}", admin)).Code);

            var hall = await RoomAsync(admin, capacity: 50);
            var withRoom = await AddRoomAsync(admin, exam, hall);
            Assert.Equal(200, withRoom.Code);
            Assert.Equal(50, withRoom.Prop("totalCapacity").GetInt32());

            Assert.Equal(200, (await AddInvigilatorAsync(admin, exam, hall, assistant, "Assistant")).Code);

            var noChief = await Client.PostAsync($"/api/v1/exams/{exam}/publish", null, admin);
            Assert.Equal(422, noChief.Code);
            Assert.Equal("EXAM_CHIEF_MISSING", noChief.ErrorCode);

            var sameAgain = await AddInvigilatorAsync(admin, exam, hall, assistant, "Chief");
            Assert.Equal(409, sameAgain.Code);
            Assert.Equal("EXAM_INVIGILATOR_EXISTS", sameAgain.ErrorCode);

            Assert.Equal(200, (await AddInvigilatorAsync(admin, exam, hall, chief, "Chief")).Code);

            var otherChief = await AcademicScenario.CreateInstructorAsync(_api, admin, scenario.Setup.DepartmentId);
            var secondChief = await AddInvigilatorAsync(admin, exam, hall, otherChief, "Chief");
            Assert.Equal(409, secondChief.Code);
            Assert.Equal("EXAM_CHIEF_EXISTS", secondChief.ErrorCode);

            var published = await Client.PostAsync($"/api/v1/exams/{exam}/publish", null, admin);
            Assert.Equal(200, published.Code);
            Assert.Equal("Published", published.Prop("status").GetString());

            // Nothing changes on a published exam.
            var update = await Client.PutAsync($"/api/v1/exams/{exam}", new { maxScore = 50m, durationMinutes = 60 }, admin);
            Assert.Equal(422, update.Code);
            Assert.Equal("EXAM_INVALID_STATE", update.ErrorCode);

            var reschedule = await ScheduleAsync(admin, exam, ExamDay, "10:00", "13:00");
            Assert.Equal(422, reschedule.Code);
            Assert.Equal("EXAM_INVALID_STATE", reschedule.ErrorCode);

            var publishAgain = await Client.PostAsync($"/api/v1/exams/{exam}/publish", null, admin);
            Assert.Equal(422, publishAgain.Code);
            Assert.Equal("EXAM_INVALID_STATE", publishAgain.ErrorCode);

            var cancelled = await Client.PostAsync($"/api/v1/exams/{exam}/cancel", null, admin);
            Assert.Equal(200, cancelled.Code);
            Assert.Equal("Cancelled", cancelled.Prop("status").GetString());

            var cancelAgain = await Client.PostAsync($"/api/v1/exams/{exam}/cancel", null, admin);
            Assert.Equal(422, cancelAgain.Code);
            Assert.Equal("EXAM_INVALID_STATE", cancelAgain.ErrorCode);
        }

        [Fact]
        public async Task Two_exams_cannot_share_a_room_or_an_invigilator_at_overlapping_times()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, courseCount: 2);
            var session = await SessionAsync(admin, setup.Calendar.SemesterId);
            var first = await NewExamAsync(admin, setup.Courses[0].SectionId, session);
            var second = await NewExamAsync(admin, setup.Courses[1].SectionId, session);
            var shared = await RoomAsync(admin, capacity: 30);
            var other = await RoomAsync(admin, capacity: 30);
            var instructor = await AcademicScenario.CreateInstructorAsync(_api, admin, setup.DepartmentId);

            Assert.Equal(200, (await ScheduleAsync(admin, first, ExamDay, "09:00", "12:00")).Code);
            Assert.Equal(200, (await ScheduleAsync(admin, second, ExamDay, "10:00", "13:00")).Code);

            Assert.Equal(200, (await AddRoomAsync(admin, first, shared)).Code);
            Assert.Equal(200, (await AddInvigilatorAsync(admin, first, shared, instructor, "Chief")).Code);

            var sameRoom = await AddRoomAsync(admin, second, shared);
            Assert.Equal(409, sameRoom.Code);
            Assert.Equal("EXAM_ROOM_CONFLICT", sameRoom.ErrorCode);

            Assert.Equal(200, (await AddRoomAsync(admin, second, other)).Code);

            var sameInvigilator = await AddInvigilatorAsync(admin, second, other, instructor, "Chief");
            Assert.Equal(409, sameInvigilator.Code);
            Assert.Equal("EXAM_INVIGILATOR_CONFLICT", sameInvigilator.ErrorCode);

            // Touching times do not overlap: the second exam can follow the first one.
            var after = await ScheduleAsync(admin, second, ExamDay, "12:00", "15:00");
            Assert.Equal(200, after.Code);
            Assert.Equal(200, (await AddInvigilatorAsync(admin, second, other, instructor, "Chief")).Code);
        }

        [Fact]
        public async Task A_student_cannot_have_two_exams_at_the_same_time()
        {
            var admin = await _api.AdminTokenAsync();
            var setup = await AcademicScenario.BuildAsync(_api, admin, courseCount: 2);
            var student = await AcademicScenario.AdmitAsync(_api, admin, setup);

            foreach (var course in setup.Courses)
            {
                var registered = await Client.PostAsync("/api/v1/registrations", new { studentId = student.Id, academicSemesterId = setup.Calendar.SemesterId, courseSectionId = course.SectionId }, admin, TestApi.NewIdempotencyKey());
                Assert.Equal(201, registered.Code);
            }

            var session = await SessionAsync(admin, setup.Calendar.SemesterId);
            var first = await NewExamAsync(admin, setup.Courses[0].SectionId, session);
            var second = await NewExamAsync(admin, setup.Courses[1].SectionId, session);

            Assert.Equal(200, (await ScheduleAsync(admin, first, ExamDay, "09:00", "12:00")).Code);

            var clash = await ScheduleAsync(admin, second, ExamDay, "11:00", "14:00");
            Assert.Equal(409, clash.Code);
            Assert.Equal("EXAM_STUDENT_CONFLICT", clash.ErrorCode);

            var next = await ScheduleAsync(admin, second, ExamDay, "13:00", "16:00");
            Assert.Equal(200, next.Code);
        }

        [Fact]
        public async Task The_roster_leaves_out_students_blocked_by_attendance()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 2);
            var present = scenario.Students[0].Id;
            var absent = scenario.Students[1].Id;

            var policy = await Client.PostAsync("/api/v1/attendance/policies", new { courseId = scenario.Setup.Courses[0].CourseId, minimumPercentage = 80m, warningPercentage = 90m, lateThresholdMinutes = 10, isExamBlocking = true }, admin);
            Assert.Equal(201, policy.Code);

            var attendance = await Client.PostAsync("/api/v1/attendance/sessions", new { courseSectionId = scenario.SectionId, sessionDate = Today.AddDays(-30), startTime = "09:00:00" }, admin);
            Assert.Equal(201, attendance.Code);
            var attendanceId = attendance.GuidOf("id");

            var marked = await Client.PostAsync($"/api/v1/attendance/sessions/{attendanceId}/marks", new { marks = new[] { new { studentId = present, status = "Present" }, new { studentId = absent, status = "Absent" } } }, admin);
            Assert.Equal(200, marked.Code);
            Assert.Equal(200, (await Client.PostAsync($"/api/v1/attendance/sessions/{attendanceId}/close", null, admin)).Code);

            var session = await SessionAsync(admin, scenario.Setup.Calendar.SemesterId);
            var exam = await NewExamAsync(admin, scenario.SectionId, session);

            var roster = await Client.GetAsync($"/api/v1/exams/{exam}/roster", admin);

            Assert.Equal(200, roster.Code);
            Assert.Equal(2, roster.Prop("registered").GetInt32());
            Assert.Equal([present], roster.Prop("eligibleStudentIds").EnumerateArray().Select(e => e.GetGuid()).ToArray());
            Assert.Equal([absent], roster.Prop("blockedStudentIds").EnumerateArray().Select(e => e.GetGuid()).ToArray());
        }

        [Fact]
        public async Task A_student_cannot_manage_exams()
        {
            var admin = await _api.AdminTokenAsync();
            var scenario = await GradingScenario.BuildAsync(_api, admin, students: 1, createAccounts: true);
            var token = await _api.LoginTokenAsync(scenario.Students[0].Number, TestApi.DefaultPassword);
            var session = await SessionAsync(admin, scenario.Setup.Calendar.SemesterId);

            var newSession = await Client.PostAsync("/api/v1/exam-sessions", new { academicSemesterId = scenario.Setup.Calendar.SemesterId, name = $"Mine {Short(10)}", examType = "Final", startDate = Today.AddDays(-20), endDate = Today.AddDays(-10) }, token);
            Assert.Equal(403, newSession.Code);

            var newExam = await CreateExamAsync(token, scenario.SectionId, session);
            Assert.Equal(403, newExam.Code);
        }

        [Fact]
        public async Task An_unknown_exam_or_session_is_not_found()
        {
            var admin = await _api.AdminTokenAsync();

            var exam = await Client.GetAsync($"/api/v1/exams/{Guid.NewGuid()}", admin);
            Assert.Equal(404, exam.Code);
            Assert.Equal("EXAM_NOT_FOUND", exam.ErrorCode);

            var session = await Client.GetAsync($"/api/v1/exam-sessions/{Guid.NewGuid()}", admin);
            Assert.Equal(404, session.Code);
            Assert.Equal("EXAM_SESSION_NOT_FOUND", session.ErrorCode);
        }
    }
}
