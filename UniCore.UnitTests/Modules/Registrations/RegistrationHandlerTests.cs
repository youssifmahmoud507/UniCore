using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Registrations;

namespace UniCore.UnitTests.Modules.Registrations
{
    public class RegistrationHandlerTests
    {
        // ---------- register ----------

        [Fact]
        public async Task Register_takes_a_seat_creates_the_registration_and_audits()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection(capacity: 2);

            var outcome = await env.RegisterOkAsync(student, section, RegistrationsTestEnv.Key);

            Assert.False(outcome.IsReplay);
            Assert.Equal(RegistrationStatus.Draft, outcome.Registration.Status);
            Assert.Equal(student.Id, outcome.Registration.StudentId);
            Assert.Equal(EnrollmentStatus.Registered, outcome.Enrollment.Status);
            Assert.Equal(3, outcome.Registration.RegisteredCredits);
            Assert.Equal(1, env.SectionOf(section).EnrolledCount);
            Assert.Single(env.Registrations.Items);
            Assert.Equal(1, env.Rbac.Uow.SaveCalls);

            var entry = Assert.Single(env.Rbac.Audit.Entries);
            Assert.Equal(RegistrationsAuditActions.EnrollmentRegistered, entry.Action);
            Assert.DoesNotContain(RegistrationsTestEnv.Key, entry.NewValues ?? string.Empty);
        }

        [Fact]
        public async Task Register_a_second_course_reuses_the_registration()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();

            var first = await env.RegisterOkAsync(student, env.SeedSection("A1"));
            var second = await env.RegisterOkAsync(student, env.SeedSection("A2"));

            Assert.Equal(first.Registration.Id, second.Registration.Id);
            Assert.Single(env.Registrations.Items);
            Assert.Equal(6, second.Registration.RegisteredCredits);
            Assert.Equal(2, second.Registration.Enrollments.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("short")]
        [InlineData("has a space in it")]
        public async Task Register_needs_a_valid_idempotency_key(string? key)
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);

            var result = await env.Handler.RegisterAsync(new RegisterCommand(student.Id, env.Semester.Id, env.SeedSection().SectionId, null), key, RegistrationsTestEnv.Request);

            Assert.Equal("IDEMPOTENCY_KEY_INVALID", result.Error?.Code);
            Assert.Empty(env.Registrations.Items);
        }

        [Fact]
        public async Task Register_rejects_empty_ids()
        {
            var env = new RegistrationsTestEnv();
            env.AsRegistrar();

            var result = await env.Handler.RegisterAsync(new RegisterCommand(Guid.Empty, env.Semester.Id, Guid.NewGuid(), null), RegistrationsTestEnv.Key, RegistrationsTestEnv.Request);

            Assert.Equal("REGISTRATION_REQUEST_INVALID", result.Error?.Code);
        }

        [Fact]
        public async Task Register_with_an_unknown_semester_fails()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);

            var result = await env.Handler.RegisterAsync(new RegisterCommand(student.Id, Guid.NewGuid(), env.SeedSection().SectionId, null), RegistrationsTestEnv.Key, RegistrationsTestEnv.Request);

            Assert.Equal("REGISTRATION_SEMESTER_NOT_FOUND", result.Error?.Code);
        }

        // ---------- idempotency ----------

        [Fact]
        public async Task Same_key_and_section_replays_without_taking_a_second_seat()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();

            var first = await env.RegisterOkAsync(student, section, RegistrationsTestEnv.Key);
            var saves = env.Rbac.Uow.SaveCalls;

            var replay = await env.RegisterAsync(student, section, RegistrationsTestEnv.Key);

            Assert.True(replay.IsSuccess);
            Assert.True(replay.Value!.IsReplay);
            Assert.Equal(first.Enrollment.Id, replay.Value.Enrollment.Id);
            Assert.Equal(1, env.SectionOf(section).EnrolledCount);
            Assert.Equal(saves, env.Rbac.Uow.SaveCalls);
            Assert.Single(env.Rbac.Audit.Entries);
        }

        [Fact]
        public async Task Replay_still_works_after_the_window_closed()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();
            await env.RegisterOkAsync(student, section, RegistrationsTestEnv.Key);

            env.AcademicClock.Today = new DateOnly(2026, 9, 15);
            var replay = await env.RegisterAsync(student, section, RegistrationsTestEnv.Key);

            Assert.True(replay.Value!.IsReplay);
        }

        [Fact]
        public async Task Same_key_for_another_section_is_reused()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            await env.RegisterOkAsync(student, env.SeedSection("A1"), RegistrationsTestEnv.Key);

            var other = await env.RegisterAsync(student, env.SeedSection("A2"), RegistrationsTestEnv.Key);

            Assert.Equal("IDEMPOTENCY_KEY_REUSED", other.Error?.Code);
        }

        // ---------- authorization ----------

        [Fact]
        public async Task Another_student_cannot_register_for_this_student()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();
            env.AsStranger();

            var result = await env.RegisterAsync(student, section);

            Assert.Equal(ErrorType.Forbidden, result.Error?.ErrorType);
            Assert.Empty(env.Registrations.Items);
            Assert.Equal(0, env.SectionOf(section).EnrolledCount);
        }

        [Fact]
        public async Task Without_the_permission_nothing_is_revealed()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            env.Rbac.SignIn(Guid.NewGuid());

            var unknown = await env.Handler.RegisterAsync(new RegisterCommand(Guid.NewGuid(), env.Semester.Id, Guid.NewGuid(), null), RegistrationsTestEnv.Key, RegistrationsTestEnv.Request);
            var known = await env.Handler.RegisterAsync(new RegisterCommand(student.Id, env.Semester.Id, Guid.NewGuid(), null), RegistrationsTestEnv.Key, RegistrationsTestEnv.Request);

            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", unknown.Error?.Code);
            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", known.Error?.Code);
        }

        [Fact]
        public async Task Staff_can_register_for_a_student()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            env.AsRegistrar();

            var result = await env.RegisterAsync(student, env.SeedSection());

            Assert.True(result.IsSuccess, result.Error?.Code);
        }

        [Fact]
        public async Task Registrar_override_skips_only_the_window_and_is_audited_with_its_reason()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();
            env.AcademicClock.Today = new DateOnly(2026, 9, 15);
            env.AsRegistrar();

            var result = await env.RegisterAsync(student, section, overrideReason: "Dean approved a late registration");

            Assert.True(result.IsSuccess, result.Error?.Code);
            var entry = Assert.Single(env.Rbac.Audit.Entries);
            Assert.Equal(RegistrationsAuditActions.RegistrarOverride, entry.Action);
            Assert.Contains("Dean approved", entry.NewValues);

            // The other checks still apply.
            env.Finance.Hold = true;
            var held = await env.RegisterAsync(student, env.SeedSection("B1"), key: "key-held", overrideReason: "Dean approved a late registration");
            Assert.Equal("FINANCIAL_HOLD", held.Error?.Code);
        }

        [Fact]
        public async Task Student_cannot_use_an_override_and_the_reason_must_be_real()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();
            env.AsStudent(student);

            var byStudent = await env.RegisterAsync(student, section, overrideReason: "please let me in");
            Assert.Equal("REGISTRATION_OVERRIDE_FORBIDDEN", byStudent.Error?.Code);

            env.AsRegistrar();
            var tooShort = await env.RegisterAsync(student, section, overrideReason: "ok");
            Assert.Equal("REGISTRATION_OVERRIDE_REASON_INVALID", tooShort.Error?.Code);
        }

        // ---------- seats and concurrency ----------

        [Fact]
        public async Task The_last_seat_goes_to_one_student_and_the_next_gets_course_full()
        {
            var env = new RegistrationsTestEnv();
            var section = env.SeedSection(capacity: 1);

            await env.RegisterOkAsync(env.SeedStudent(), section);

            var late = env.SeedStudent();
            env.AsStudent(late);
            var refused = await env.RegisterAsync(late, section);

            Assert.Equal("COURSE_FULL", refused.Error?.Code);
            Assert.Equal(1, env.SectionOf(section).EnrolledCount);
        }

        // Sequential model of "N requests for the last seats": with capacity 3, exactly three of ten students get a seat.
        // The truly parallel version needs SQL Server (the RowVersion on CourseSection) and belongs to the integration tests.
        [Fact]
        public async Task Only_capacity_many_of_ten_students_get_a_seat()
        {
            var env = new RegistrationsTestEnv();
            var section = env.SeedSection(capacity: 3);
            var codes = new List<string?>();

            for (var i = 0; i < 10; i++)
            {
                var student = env.SeedStudent();
                env.AsStudent(student);
                var result = await env.RegisterAsync(student, section, "key-" + Guid.NewGuid().ToString("N"));
                codes.Add(result.IsSuccess ? null : result.Error?.Code);
            }

            Assert.Equal(3, codes.Count(c => c is null));
            Assert.Equal(7, codes.Count(c => c == "COURSE_FULL"));
            Assert.Equal(3, env.SectionOf(section).EnrolledCount);
        }

        [Fact]
        public async Task A_concurrency_conflict_on_save_is_reported_so_the_client_can_retry()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            env.AsStudent(student);
            env.Rbac.Uow.FailWith = UniCore.Domain.Common.Results.CommonErrors.ConcurrencyConflict;

            var result = await env.RegisterAsync(student, env.SeedSection());

            Assert.Equal("CONCURRENCY_CONFLICT", result.Error?.Code);
            Assert.Empty(env.Rbac.Audit.Entries);
        }

        // ---------- submit / cancel ----------

        [Fact]
        public async Task Submit_needs_a_course_then_moves_to_submitted()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var outcome = await env.RegisterOkAsync(student, env.SeedSection());

            var submitted = await env.Handler.SubmitAsync(outcome.Registration.Id, RegistrationsTestEnv.Request);

            Assert.True(submitted.IsSuccess, submitted.Error?.Code);
            Assert.Equal(RegistrationStatus.Submitted, submitted.Value!.Status);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == RegistrationsAuditActions.RegistrationSubmitted);

            Assert.Equal("REGISTRATION_INVALID_STATE", (await env.Handler.SubmitAsync(outcome.Registration.Id, RegistrationsTestEnv.Request)).Error?.Code);
        }

        [Fact]
        public async Task Submit_of_an_empty_registration_fails()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var outcome = await env.RegisterOkAsync(student, env.SeedSection());
            var drop = await env.Handler.DropAsync(outcome.Registration.Id, new DropCommand([outcome.Enrollment.Id]), RegistrationsTestEnv.Request);
            Assert.True(drop.IsSuccess, drop.Error?.Code);

            Assert.Equal("REGISTRATION_EMPTY", (await env.Handler.SubmitAsync(outcome.Registration.Id, RegistrationsTestEnv.Request)).Error?.Code);
        }

        [Fact]
        public async Task Another_student_cannot_submit_or_read_the_registration()
        {
            var env = new RegistrationsTestEnv();
            var outcome = await env.RegisterOkAsync(env.SeedStudent(), env.SeedSection());
            env.AsStranger();

            Assert.Equal(ErrorType.Forbidden, (await env.Handler.SubmitAsync(outcome.Registration.Id, RegistrationsTestEnv.Request)).Error?.ErrorType);
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.GetAsync(outcome.Registration.Id)).Error?.ErrorType);
            Assert.Equal("REGISTRATION_NOT_FOUND", (await env.Handler.GetAsync(Guid.NewGuid())).Error?.Code);
        }

        [Fact]
        public async Task Cancel_releases_every_seat()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var a = env.SeedSection("A1");
            var b = env.SeedSection("A2");
            await env.RegisterOkAsync(student, a);
            var outcome = await env.RegisterOkAsync(student, b);

            var cancelled = await env.Handler.CancelAsync(outcome.Registration.Id, RegistrationsTestEnv.Request);

            Assert.True(cancelled.IsSuccess, cancelled.Error?.Code);
            Assert.Equal(RegistrationStatus.Cancelled, cancelled.Value!.Status);
            Assert.Equal(0, env.SectionOf(a).EnrolledCount);
            Assert.Equal(0, env.SectionOf(b).EnrolledCount);
            Assert.Equal(0, cancelled.Value.RegisteredCredits);
        }

        [Fact]
        public async Task A_student_cannot_cancel_after_the_registration_window_but_staff_can()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();
            var outcome = await env.RegisterOkAsync(student, section);

            env.AcademicClock.Today = new DateOnly(2026, 9, 15);

            Assert.Equal("REGISTRATION_CLOSED", (await env.Handler.CancelAsync(outcome.Registration.Id, RegistrationsTestEnv.Request)).Error?.Code);
            Assert.Equal(1, env.SectionOf(section).EnrolledCount);

            env.AsRegistrar();
            var staff = await env.Handler.CancelAsync(outcome.Registration.Id, RegistrationsTestEnv.Request);

            Assert.True(staff.IsSuccess, staff.Error?.Code);
            Assert.Equal(0, env.SectionOf(section).EnrolledCount);
        }

        [Fact]
        public async Task A_cancelled_registration_accepts_no_more_courses()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var outcome = await env.RegisterOkAsync(student, env.SeedSection("A1"));
            await env.Handler.CancelAsync(outcome.Registration.Id, RegistrationsTestEnv.Request);

            var again = await env.RegisterAsync(student, env.SeedSection("A2"), "key-" + Guid.NewGuid().ToString("N"));

            Assert.Equal("REGISTRATION_INVALID_STATE", again.Error?.Code);
        }

        // ---------- drop ----------

        [Fact]
        public async Task Drop_in_the_add_drop_period_releases_the_seat()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();
            var outcome = await env.RegisterOkAsync(student, section);

            env.AcademicClock.Today = new DateOnly(2026, 9, 15);

            var dropped = await env.Handler.DropAsync(outcome.Registration.Id, new DropCommand([outcome.Enrollment.Id]), RegistrationsTestEnv.Request);

            Assert.True(dropped.IsSuccess, dropped.Error?.Code);
            Assert.Equal(EnrollmentStatus.Dropped, dropped.Value!.Enrollments[0].Status);
            Assert.Equal(0, env.SectionOf(section).EnrolledCount);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == RegistrationsAuditActions.EnrollmentDropped);
        }

        [Theory]
        [InlineData(2026, 9, 20, true)]
        [InlineData(2026, 9, 21, false)]
        public async Task Drop_is_allowed_up_to_the_last_add_drop_day(int year, int month, int day, bool allowed)
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var outcome = await env.RegisterOkAsync(student, env.SeedSection());

            env.AcademicClock.Today = new DateOnly(year, month, day);
            var result = await env.Handler.DropAsync(outcome.Registration.Id, new DropCommand([outcome.Enrollment.Id]), RegistrationsTestEnv.Request);

            Assert.Equal(allowed, result.IsSuccess);
            if (!allowed)
            {
                Assert.Equal("ADD_DROP_CLOSED", result.Error?.Code);
            }
        }

        [Fact]
        public async Task Dropping_twice_is_a_harmless_no_op()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();
            var outcome = await env.RegisterOkAsync(student, section);
            var command = new DropCommand([outcome.Enrollment.Id]);

            await env.Handler.DropAsync(outcome.Registration.Id, command, RegistrationsTestEnv.Request);
            var saves = env.Rbac.Uow.SaveCalls;

            var again = await env.Handler.DropAsync(outcome.Registration.Id, command, RegistrationsTestEnv.Request);

            Assert.True(again.IsSuccess);
            Assert.Equal(saves, env.Rbac.Uow.SaveCalls);
            Assert.Equal(0, env.SectionOf(section).EnrolledCount);
        }

        [Fact]
        public async Task Drop_needs_enrollments_that_belong_to_the_registration()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var outcome = await env.RegisterOkAsync(student, env.SeedSection());

            Assert.Equal("REGISTRATION_ENROLLMENTS_REQUIRED", (await env.Handler.DropAsync(outcome.Registration.Id, new DropCommand([]), RegistrationsTestEnv.Request)).Error?.Code);
            Assert.Equal("ENROLLMENT_NOT_FOUND", (await env.Handler.DropAsync(outcome.Registration.Id, new DropCommand([Guid.NewGuid()]), RegistrationsTestEnv.Request)).Error?.Code);
        }

        [Fact]
        public async Task A_course_needed_as_corequisite_cannot_be_dropped_alone()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var lecture = env.SeedSection("PH101");
            var lab = env.SeedSection("PH101L");
            env.Prerequisites.Corequisites[lab.CourseVersionId] = [lecture.CourseId];

            var first = await env.RegisterOkAsync(student, lecture);
            var second = await env.RegisterOkAsync(student, lab);

            var alone = await env.Handler.DropAsync(first.Registration.Id, new DropCommand([first.Enrollment.Id]), RegistrationsTestEnv.Request);
            Assert.Equal("COREQUISITE_DROP_BLOCKED", alone.Error?.Code);
            Assert.Equal(1, env.SectionOf(lecture).EnrolledCount);

            var together = await env.Handler.DropAsync(first.Registration.Id, new DropCommand([first.Enrollment.Id, second.Enrollment.Id]), RegistrationsTestEnv.Request);
            Assert.True(together.IsSuccess, together.Error?.Code);
            Assert.Equal(0, env.SectionOf(lecture).EnrolledCount);
            Assert.Equal(0, env.SectionOf(lab).EnrolledCount);
        }

        [Fact]
        public async Task A_suspended_student_cannot_drop()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var outcome = await env.RegisterOkAsync(student, env.SeedSection());
            env.Suspend(student);
            env.AsStudent(student);

            var result = await env.Handler.DropAsync(outcome.Registration.Id, new DropCommand([outcome.Enrollment.Id]), RegistrationsTestEnv.Request);

            Assert.Equal("STUDENT_INACTIVE", result.Error?.Code);
        }

        [Fact]
        public async Task Registering_again_after_a_drop_reuses_the_enrollment_and_retakes_the_seat()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();
            var first = await env.RegisterOkAsync(student, section);
            await env.Handler.DropAsync(first.Registration.Id, new DropCommand([first.Enrollment.Id]), RegistrationsTestEnv.Request);

            var second = await env.RegisterOkAsync(student, section);

            Assert.Equal(first.Enrollment.Id, second.Enrollment.Id);
            Assert.Single(second.Registration.Enrollments);
            Assert.Equal(1, env.SectionOf(section).EnrolledCount);
        }

        // ---------- withdraw ----------

        [Theory]
        [InlineData(2026, 9, 15, false)]
        [InlineData(2026, 9, 20, false)]
        [InlineData(2026, 9, 21, true)]
        [InlineData(2026, 11, 15, true)]
        [InlineData(2026, 11, 16, false)]
        public async Task Withdraw_is_open_after_add_drop_until_the_deadline(int year, int month, int day, bool allowed)
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();
            var outcome = await env.RegisterOkAsync(student, section);

            env.AcademicClock.Today = new DateOnly(year, month, day);
            var result = await env.Handler.WithdrawAsync(outcome.Registration.Id, outcome.Enrollment.Id, RegistrationsTestEnv.Request);

            Assert.Equal(allowed, result.IsSuccess);

            if (allowed)
            {
                Assert.Equal(EnrollmentStatus.Withdrawn, result.Value!.Enrollments[0].Status);
                Assert.Equal(0, env.SectionOf(section).EnrolledCount);
                Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == RegistrationsAuditActions.EnrollmentWithdrawn);
            }
            else
            {
                Assert.Equal("WITHDRAWAL_DEADLINE_PASSED", result.Error?.Code);
                Assert.Equal(1, env.SectionOf(section).EnrolledCount);
            }
        }

        [Fact]
        public async Task A_withdrawn_course_cannot_be_registered_again()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var section = env.SeedSection();
            var outcome = await env.RegisterOkAsync(student, section);
            env.AcademicClock.Today = new DateOnly(2026, 9, 25);
            await env.Handler.WithdrawAsync(outcome.Registration.Id, outcome.Enrollment.Id, RegistrationsTestEnv.Request);

            env.AcademicClock.Today = new DateOnly(2026, 9, 5);
            var again = await env.RegisterAsync(student, section, "key-" + Guid.NewGuid().ToString("N"));

            Assert.Equal("ENROLLMENT_INVALID_STATE", again.Error?.Code);
        }

        // ---------- reading ----------

        [Fact]
        public async Task The_student_reads_their_own_registration_and_list()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var outcome = await env.RegisterOkAsync(student, env.SeedSection());

            env.AsStudent(student);

            var mine = await env.Handler.GetMineAsync(env.Semester.Id);
            Assert.True(mine.IsSuccess, mine.Error?.Code);
            Assert.Equal(outcome.Registration.Id, mine.Value!.Id);

            var list = await env.Handler.ListAsync(student.Id, null, null, null, new UniCore.Application.Common.Pagination.PaginationQuery());
            Assert.True(list.IsSuccess, list.Error?.Code);
            Assert.Single(list.Value!.Items);

            Assert.Equal("REGISTRATION_NOT_FOUND", (await env.Handler.GetMineAsync(Guid.NewGuid())).Error?.Code);
        }

        [Fact]
        public async Task Manual_complete_is_for_staff_only()
        {
            var env = new RegistrationsTestEnv();
            var student = env.SeedStudent();
            var outcome = await env.RegisterOkAsync(student, env.SeedSection());

            env.AsStudent(student);
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.CompleteAsync(outcome.Registration.Id, RegistrationsTestEnv.Request)).Error?.ErrorType);

            env.AsRegistrar();
            var done = await env.Handler.CompleteAsync(outcome.Registration.Id, RegistrationsTestEnv.Request);
            Assert.True(done.IsSuccess, done.Error?.Code);
            Assert.Equal(RegistrationStatus.Completed, done.Value!.Status);
        }

        // ---------- semester close ----------

        [Fact]
        public async Task Completion_service_completes_registrations_of_closed_semesters_only()
        {
            var env = new RegistrationsTestEnv();
            var closed = env.SeedSemester(closed: true);

            var open = ResultOf(Registration.Create(env.SeedStudent().Id, env.Semester.Id, env.Clock.UtcNow));
            var old = ResultOf(Registration.Create(env.SeedStudent().Id, closed.Id, env.Clock.UtcNow));
            Assert.True(old.Enroll(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, null, env.Clock.UtcNow).IsSuccess);
            env.Registrations.Items.AddRange([open, old]);

            var outcome = await env.Completion.RunAsync();

            Assert.Equal(1, outcome.Value!.Completed);
            Assert.Equal(RegistrationStatus.Completed, old.Status);
            Assert.Equal(EnrollmentStatus.Completed, old.Enrollments[0].Status);
            Assert.Equal(RegistrationStatus.Draft, open.Status);

            // Running it again changes nothing.
            Assert.Equal(0, (await env.Completion.RunAsync()).Value!.Completed);
        }

        private static Registration ResultOf(Result<Registration> result)
        {
            Assert.True(result.IsSuccess, result.Error?.Code);
            return result.Value!;
        }

        // ---------- the contract other modules use ----------

        [Fact]
        public async Task Read_service_lists_enrollments_and_the_section_roster()
        {
            var env = new RegistrationsTestEnv();
            var section = env.SeedSection();
            var a = env.SeedStudent();
            var b = env.SeedStudent();
            await env.RegisterOkAsync(a, section);
            var second = await env.RegisterOkAsync(b, section);

            env.AsStudent(b);
            await env.Handler.DropAsync(second.Registration.Id, new DropCommand([second.Enrollment.Id]), RegistrationsTestEnv.Request);

            var roster = await env.Reads.GetRosterAsync(section.SectionId);
            var entry = Assert.Single(roster.Value!);
            Assert.Equal(a.Id, entry.StudentId);

            var history = await env.Reads.GetStudentEnrollmentsAsync(b.Id, env.Semester.Id);
            Assert.Equal(EnrollmentStatus.Dropped, Assert.Single(history.Value!).Status);
        }
    }
}
