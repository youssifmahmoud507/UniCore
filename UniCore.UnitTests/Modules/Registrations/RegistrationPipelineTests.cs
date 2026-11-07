using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.Students;

namespace UniCore.UnitTests.Modules.Registrations
{
    // One test per step of the pipeline, and the order of the steps (registration-rules.md section 2).
    public class RegistrationPipelineTests
    {
        private static async Task<string?> RegisterCodeAsync(RegistrationsTestEnv env, Student student, RegistrationsTestEnv.SeededSection section, string? key = null)
        {
            env.AsStudent(student);
            var result = await env.RegisterAsync(student, section, key ?? "key-" + Guid.NewGuid().ToString("N"));
            return result.IsSuccess ? null : result.Error?.Code;
        }

        // ---------- step 0: the section ----------

        [Fact]
        public async Task Unknown_section_is_not_found()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var ghost = new RegistrationsTestEnv.SeededSection(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3);

            Assert.Equal("SECTION_NOT_FOUND", await RegisterCodeAsync(env, student, ghost));
        }

        [Fact]
        public async Task Section_of_another_semester_is_a_mismatch()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var other = env.SeedSemester();
            var section = env.SeedSection(semester: other);

            Assert.Equal("SECTION_SEMESTER_MISMATCH", await RegisterCodeAsync(env, student, section));
        }

        [Fact]
        public async Task Closed_section_and_section_without_a_primary_instructor_are_not_open()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();

            var closed = env.SeedSection("CS101");
            env.SectionOf(closed).Close(env.Clock.UtcNow);
            Assert.Equal("SECTION_NOT_OPEN", await RegisterCodeAsync(env, student, closed));

            var untaught = env.SeedSection("CS102", hasPrimary: false);
            Assert.Equal("SECTION_NOT_OPEN", await RegisterCodeAsync(env, student, untaught));
        }

        // ---------- steps 1 to 9 ----------

        [Fact]
        public async Task Inactive_student_cannot_register()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            env.Suspend(student);

            Assert.Equal("STUDENT_INACTIVE", await RegisterCodeAsync(env, student, env.SeedSection()));
        }

        [Fact]
        public async Task Registration_window_must_be_open()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();

            env.AcademicClock.Today = new DateOnly(2026, 9, 15);
            Assert.Equal("REGISTRATION_CLOSED", await RegisterCodeAsync(env, student, section));

            env.AcademicClock.Today = new DateOnly(2026, 8, 31);
            Assert.Equal("REGISTRATION_CLOSED", await RegisterCodeAsync(env, student, section));

            env.AcademicClock.Today = new DateOnly(2026, 9, 10);
            Assert.Null(await RegisterCodeAsync(env, student, section));
        }

        [Fact]
        public async Task Financial_hold_blocks_registration()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            env.Finance.Hold = true;

            Assert.Equal("FINANCIAL_HOLD", await RegisterCodeAsync(env, student, env.SeedSection()));
        }

        [Fact]
        public async Task Course_must_be_in_the_study_plan()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();

            Assert.Equal("COURSE_NOT_IN_STUDY_PLAN", await RegisterCodeAsync(env, student, env.SeedSection(inPlan: false)));
        }

        [Fact]
        public async Task A_newer_version_of_a_planned_course_is_accepted_through_the_course()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var planned = env.SeedSection("CS101");

            // Same course, another version that the plan does not list.
            var newer = env.SeedSection("CS101", inPlan: false, courseId: planned.CourseId);

            Assert.Null(await RegisterCodeAsync(env, student, newer));
        }

        [Fact]
        public async Task Unmet_prerequisite_blocks_registration()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();
            env.Prerequisites.Unmet.Add(section.CourseVersionId);

            Assert.Equal("PREREQUISITE_NOT_MET", await RegisterCodeAsync(env, student, section));
        }

        [Fact]
        public async Task Corequisite_must_be_passed_or_registered_in_the_same_semester()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var lecture = env.SeedSection("PH101");
            var lab = env.SeedSection("PH101L");
            env.Prerequisites.Corequisites[lab.CourseVersionId] = [lecture.CourseId];

            Assert.Equal("COREQUISITE_NOT_MET", await RegisterCodeAsync(env, student, lab));

            await env.RegisterOkAsync(student, lecture);
            Assert.Null(await RegisterCodeAsync(env, student, lab));
        }

        [Fact]
        public async Task Passed_corequisite_is_enough()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var lecture = env.SeedSection("PH101");
            var lab = env.SeedSection("PH101L");
            env.Prerequisites.Corequisites[lab.CourseVersionId] = [lecture.CourseId];
            env.History.Attempts[lecture.CourseId] = new CourseAttemptSummary(true, 3.0m, 1);

            Assert.Null(await RegisterCodeAsync(env, student, lab));
        }

        [Fact]
        public async Task Passed_course_is_refused_unless_the_grade_can_be_improved()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();

            env.History.Attempts[section.CourseId] = new CourseAttemptSummary(true, 3.5m, 1);
            Assert.Equal("COURSE_ALREADY_PASSED", await RegisterCodeAsync(env, student, section));

            // Passed with 1.0, below the improvement ceiling of 2.0.
            env.History.Attempts[section.CourseId] = new CourseAttemptSummary(true, 1.0m, 1);
            Assert.Null(await RegisterCodeAsync(env, student, section));
        }

        [Fact]
        public async Task Improvement_can_be_switched_off()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();
            env.Settings.AllowGradeImprovement = false;
            env.History.Attempts[section.CourseId] = new CourseAttemptSummary(true, 1.0m, 1);

            Assert.Equal("COURSE_ALREADY_PASSED", await RegisterCodeAsync(env, student, section));
        }

        [Fact]
        public async Task Second_section_of_the_same_course_is_already_enrolled()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var first = env.SeedSection("CS101");
            var second = env.SeedSection("CS101", courseId: first.CourseId);

            await env.RegisterOkAsync(student, first);

            Assert.Equal("ALREADY_ENROLLED", await RegisterCodeAsync(env, student, second));
        }

        [Fact]
        public async Task Regular_credit_limit_is_18()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();

            for (var i = 0; i < 6; i++)
            {
                await env.RegisterOkAsync(student, env.SeedSection("C" + i, credits: 3));
            }

            Assert.Equal("CREDIT_LIMIT_EXCEEDED", await RegisterCodeAsync(env, student, env.SeedSection("C7", credits: 3)));
        }

        [Fact]
        public async Task Probation_limit_is_12()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent(AcademicStanding.Probation);

            for (var i = 0; i < 4; i++)
            {
                await env.RegisterOkAsync(student, env.SeedSection("C" + i, credits: 3));
            }

            Assert.Equal("CREDIT_LIMIT_EXCEEDED", await RegisterCodeAsync(env, student, env.SeedSection("C9", credits: 3)));
        }

        [Fact]
        public async Task Summer_limit_is_9()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var summer = env.SeedSemester("SUMMER");

            for (var i = 0; i < 3; i++)
            {
                var section = env.SeedSection("S" + i, credits: 3, semester: summer);
                env.AsStudent(student);
                var result = await env.RegisterAsync(student, section, "key-" + Guid.NewGuid().ToString("N"), semester: summer);
                Assert.True(result.IsSuccess, result.Error?.Code);
            }

            var fourth = env.SeedSection("S9", credits: 3, semester: summer);
            var refused = await env.RegisterAsync(student, fourth, "key-" + Guid.NewGuid().ToString("N"), semester: summer);

            Assert.Equal("CREDIT_LIMIT_EXCEEDED", refused.Error?.Code);
        }

        [Fact]
        public async Task Dropped_credits_free_the_limit()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent(AcademicStanding.Probation);
            var outcomes = new List<RegisterOutcome>();

            for (var i = 0; i < 4; i++)
            {
                outcomes.Add(await env.RegisterOkAsync(student, env.SeedSection("C" + i, credits: 3)));
            }

            env.AsStudent(student);
            var registrationId = outcomes[0].Registration.Id;
            Assert.True((await env.Handler.DropAsync(registrationId, new DropCommand([outcomes[0].Enrollment.Id]), RegistrationsTestEnv.Request)).IsSuccess);

            Assert.Null(await RegisterCodeAsync(env, student, env.SeedSection("C9", credits: 3)));
        }

        [Fact]
        public async Task Schedule_conflict_blocks_registration()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            env.Schedule.Conflict = true;

            Assert.Equal("SCHEDULE_CONFLICT", await RegisterCodeAsync(env, student, env.SeedSection()));
        }

        // ---------- the order ----------

        [Fact]
        public async Task The_earliest_failing_step_is_the_one_reported()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection(inPlan: false);

            // Everything is wrong at once; fixing one problem at a time shows the order.
            env.Suspend(student);
            env.AcademicClock.Today = new DateOnly(2026, 9, 15);
            env.Finance.Hold = true;
            env.Prerequisites.Unmet.Add(section.CourseVersionId);
            env.History.Attempts[section.CourseId] = new CourseAttemptSummary(true, 4.0m, 1);
            env.Schedule.Conflict = true;
            env.SectionOf(section).Close(env.Clock.UtcNow);

            Assert.Equal("SECTION_NOT_OPEN", await RegisterCodeAsync(env, student, section));

            env.SectionOf(section).Reopen(env.Clock.UtcNow);
            Assert.Equal("STUDENT_INACTIVE", await RegisterCodeAsync(env, student, section));

            ResultAssertActivate(env, student);
            Assert.Equal("REGISTRATION_CLOSED", await RegisterCodeAsync(env, student, section));

            env.AcademicClock.Today = new DateOnly(2026, 9, 5);
            Assert.Equal("FINANCIAL_HOLD", await RegisterCodeAsync(env, student, section));

            env.Finance.Hold = false;
            Assert.Equal("COURSE_NOT_IN_STUDY_PLAN", await RegisterCodeAsync(env, student, section));

            env.Plans.CourseVersions.Add(section.CourseVersionId);
            Assert.Equal("PREREQUISITE_NOT_MET", await RegisterCodeAsync(env, student, section));

            env.Prerequisites.Unmet.Clear();
            Assert.Equal("COURSE_ALREADY_PASSED", await RegisterCodeAsync(env, student, section));

            env.History.Attempts.Clear();
            Assert.Equal("SCHEDULE_CONFLICT", await RegisterCodeAsync(env, student, section));

            env.Schedule.Conflict = false;
            Assert.Null(await RegisterCodeAsync(env, student, section));
        }

        private static void ResultAssertActivate(RegistrationsTestEnv env, Student student)
            => Assert.True(student.ChangeStatus(StudentAcademicStatus.Active, new DateOnly(2026, 9, 3), "back", Guid.NewGuid(), new DateOnly(2026, 9, 3), env.Clock.UtcNow).IsSuccess);

        [Fact]
        public async Task A_failed_check_changes_nothing()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();
            env.Finance.Hold = true;

            Assert.Equal("FINANCIAL_HOLD", await RegisterCodeAsync(env, student, section));

            Assert.Empty(env.Registrations.Items);
            Assert.Equal(0, env.SectionOf(section).EnrolledCount);
            Assert.Equal(0, env.Rbac.Uow.SaveCalls);
            Assert.Empty(env.Rbac.Audit.Entries);
        }
    }
}
