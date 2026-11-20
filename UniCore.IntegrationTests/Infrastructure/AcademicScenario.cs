using System.Text.Json;

namespace UniCore.IntegrationTests.Infrastructure
{
    // Builds a REAL academic chain through the API: university -> faculty -> department -> program,
    // one shared calendar (years cannot overlap), courses with active versions, a study plan that contains them,
    // an instructor, an open offering with one section, and students admitted into that plan.
    public static class AcademicScenario
    {
        public sealed record Calendar(Guid YearId, Guid SemesterId);

        public sealed record OfferedCourse(Guid CourseId, Guid VersionId, Guid OfferingId, Guid SectionId, int Credits);

        public sealed record Setup(Guid DepartmentId, Guid ProgramId, string ProgramCode, Guid PlanVersionId, Calendar Calendar, IReadOnlyList<OfferedCourse> Courses);

        public sealed record AdmittedStudent(Guid Id, string Number, string Email, Guid PersonId);

        private static readonly SemaphoreSlim CalendarLock = new(1, 1);
        private static Calendar? _calendar;

        private static DateOnly Today => AdmitSpec.Today;

        // Versions and plans must be effective before the semester starts (Today - 100), or the offering is refused.
        private static DateOnly EffectiveFrom => Today.AddDays(-150);

        private static string Short(int length = 8) => Guid.NewGuid().ToString("N")[..length].ToUpperInvariant();

        // One year and one semester whose registration window contains today, created once per test run.
        public static async Task<Calendar> EnsureCalendarAsync(TestApi api, string admin)
        {
            await CalendarLock.WaitAsync();
            try
            {
                if (_calendar is not null)
                {
                    return _calendar;
                }

                var client = api.Client;
                var yearStart = Today.AddDays(-200);
                var yearEnd = Today.AddDays(400);

                var year = await client.PostAsync("/api/v1/academic-years", new { name = $"Reg {Short(6)}", startDate = yearStart, endDate = yearEnd }, admin);
                Assert.Equal(201, year.Code);

                var term = await client.PostAsync("/api/v1/academic-terms", new { code = $"R{Short(6)}", name = "Registration term", sortOrder = 1 }, admin);
                Assert.Equal(201, term.Code);

                var windows = new
                {
                    registrationStart = Today.AddDays(-50),
                    registrationEnd = Today.AddDays(100),
                    addDropStart = Today.AddDays(-50),
                    addDropEnd = Today.AddDays(110),
                    withdrawalDeadline = Today.AddDays(200)
                };

                var semester = await client.PostAsync("/api/v1/semesters", new
                {
                    academicYearId = year.GuidOf("id"),
                    academicTermId = term.GuidOf("id"),
                    startDate = Today.AddDays(-100),
                    endDate = Today.AddDays(300),
                    windows
                }, admin);
                Assert.Equal(201, semester.Code);

                var advanced = await client.PostAsync($"/api/v1/semesters/{semester.GuidOf("id")}/advance", new { status = "RegistrationOpen" }, admin);
                Assert.Equal(200, advanced.Code);

                _calendar = new Calendar(year.GuidOf("id"), semester.GuidOf("id"));
                return _calendar;
            }
            finally
            {
                CalendarLock.Release();
            }
        }

        // Courses are all in one new program and study plan. credits is per course; capacity is per section.
        public static async Task<Setup> BuildAsync(TestApi api, string admin, int courseCount = 1, int credits = 3, int capacity = 30)
        {
            var calendar = await EnsureCalendarAsync(api, admin);
            var (departmentId, programId, programCode) = await CreateProgramAsync(api, admin);
            var instructorId = await CreateInstructorAsync(api, admin, departmentId);

            var courses = new List<OfferedCourse>();
            for (var i = 0; i < courseCount; i++)
            {
                courses.Add(await CreateOfferedCourseAsync(api, admin, departmentId, calendar.SemesterId, instructorId, credits, capacity));
            }

            var planVersionId = await CreatePlanAsync(api, admin, programId, courses.Select(c => c.VersionId));

            return new Setup(departmentId, programId, programCode, planVersionId, calendar, courses);
        }

        public static async Task<(Guid DepartmentId, Guid ProgramId, string ProgramCode)> CreateProgramAsync(TestApi api, string admin)
        {
            var client = api.Client;

            // The university name is unique in the database, so every scenario gets its own.
            var university = await client.PostAsync("/api/v1/universities", new { code = $"U{Short(7)}", name = $"Test University {Short(8)}", description = "x", email = "u@uni.test", phone = "0100000000", website = "https://uni.test" }, admin);
            Assert.Equal(201, university.Code);

            var faculty = await client.PostAsync("/api/v1/faculties", new { universityId = university.GuidOf("id"), code = $"F{Short(7)}", name = "Test Faculty" }, admin);
            Assert.Equal(201, faculty.Code);

            var department = await client.PostAsync("/api/v1/departments", new { facultyId = faculty.GuidOf("id"), code = $"D{Short(7)}", name = "Test Department" }, admin);
            Assert.Equal(201, department.Code);

            var code = Short(6);
            var program = await client.PostAsync("/api/v1/academic-programs", new { departmentId = department.GuidOf("id"), code, name = "Test Program", degreeType = "Bachelor", durationYears = 4, totalRequiredCredits = 140 }, admin);
            Assert.Equal(201, program.Code);

            return (department.GuidOf("id"), program.GuidOf("id"), code);
        }

        public static async Task<Guid> CreateInstructorAsync(TestApi api, string admin, Guid departmentId)
        {
            var client = api.Client;

            var person = await client.PostAsync("/api/v1/people", new { firstName = "Ahmed", lastName = "Teacher", dateOfBirth = new DateOnly(1980, 1, 1), gender = "Male", email = $"{Short(10)}@uni.test" }, admin);
            Assert.Equal(201, person.Code);

            var employee = await client.PostAsync("/api/v1/employees", new { personProfileId = person.GuidOf("id"), employeeNumber = $"E{Short(7)}", departmentId, jobTitle = "Lecturer", hireDate = Today.AddYears(-3) }, admin);
            Assert.Equal(201, employee.Code);

            var instructor = await client.PostAsync("/api/v1/instructors", new { employeeId = employee.GuidOf("id"), rank = "Lecturer", specialization = "General" }, admin);
            Assert.Equal(201, instructor.Code);

            return instructor.GuidOf("id");
        }

        // Course -> active version -> offering -> one section with a primary instructor -> offering opened.
        public static async Task<OfferedCourse> CreateOfferedCourseAsync(TestApi api, string admin, Guid departmentId, Guid semesterId, Guid instructorId, int credits, int capacity)
        {
            var client = api.Client;

            var course = await client.PostAsync("/api/v1/courses", new { departmentId, code = $"C{Short(7)}", title = "Test Course", description = "x", courseType = "Theory" }, admin);
            Assert.Equal(201, course.Code);
            var courseId = course.GuidOf("id");

            var version = await client.PostAsync($"/api/v1/courses/{courseId}/versions", new { title = "Test Course", description = "x", credits, lectureHours = 3, practicalHours = 0, effectiveFrom = EffectiveFrom }, admin);
            Assert.Equal(201, version.Code);
            var versionId = version.GuidOf("id");

            Assert.Equal(200, (await client.PostAsync($"/api/v1/course-versions/{versionId}/activate", null, admin)).Code);

            var offering = await client.PostAsync("/api/v1/course-offerings", new { courseVersionId = versionId, academicSemesterId = semesterId, maxSections = 2 }, admin);
            Assert.Equal(201, offering.Code);
            var offeringId = offering.GuidOf("id");

            var section = await client.PostAsync($"/api/v1/course-offerings/{offeringId}/sections", new { sectionCode = "S1", capacity }, admin);
            Assert.Equal(201, section.Code);
            var sectionId = section.GuidOf("id");

            // Assigning an instructor answers 201 Created (the other state changes answer 200).
            var assigned = await client.PostAsync($"/api/v1/course-sections/{sectionId}/assignments", new { instructorId, role = "Primary" }, admin);
            Assert.Equal(201, assigned.Code);

            Assert.Equal(200, (await client.PostAsync($"/api/v1/course-offerings/{offeringId}/open", null, admin)).Code);

            return new OfferedCourse(courseId, versionId, offeringId, sectionId, credits);
        }

        public static async Task<Guid> CreatePlanAsync(TestApi api, string admin, Guid programId, IEnumerable<Guid> courseVersionIds)
        {
            var client = api.Client;

            var plan = await client.PostAsync("/api/v1/study-plans", new { academicProgramId = programId, code = $"P{Short(7)}", name = "Test Plan" }, admin);
            Assert.Equal(201, plan.Code);

            var version = await client.PostAsync($"/api/v1/study-plans/{plan.GuidOf("id")}/versions", new { effectiveFrom = EffectiveFrom }, admin);
            Assert.Equal(201, version.Code);
            var versionId = version.Prop("version").GetProperty("id").GetGuid();

            foreach (var courseVersionId in courseVersionIds)
            {
                var added = await client.PostAsync($"/api/v1/study-plan-versions/{versionId}/courses", new { courseVersionId, requirementType = "Required" }, admin);
                Assert.Equal(200, added.Code);
            }

            Assert.Equal(200, (await client.PostAsync($"/api/v1/study-plan-versions/{versionId}/activate", null, admin)).Code);

            return versionId;
        }

        // A student of the setup's program and plan. With createAccount the caller logs in with Number.
        public static async Task<AdmittedStudent> AdmitAsync(TestApi api, string admin, Setup setup, bool createAccount = false)
        {
            var person = Guid.NewGuid();
            var email = $"{Short(10).ToLowerInvariant()}@uni.test";
            var admission = Today.AddDays(-30);

            var admitted = await api.Client.PostAsync("/api/v1/students", new
            {
                personProfileId = person,
                email,
                admissionDate = admission,
                academicProgramId = setup.ProgramId,
                programCode = setup.ProgramCode,
                studyPlanVersionId = setup.PlanVersionId,
                admissionAcademicYearId = setup.Calendar.YearId,
                admissionYear = admission.Year,
                createAccount
            }, admin);

            Assert.Equal(201, admitted.Code);

            JsonElement summary = admitted.Prop("student").GetProperty("summary");
            var student = new AdmittedStudent(summary.GetProperty("id").GetGuid(), summary.GetProperty("studentNumber").GetString() ?? string.Empty, email, person);

            if (createAccount)
            {
                await api.SetPasswordAsync(email, TestApi.DefaultPassword);
            }

            return student;
        }
    }
}
