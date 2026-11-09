using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Examination;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Examination;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Examination
{
    public class ExaminationHandlerTests
    {
        private static readonly RequestInfo Request = ExaminationTestEnv.Request;
        private static readonly DateOnly Day = ExaminationTestEnv.Day1;

        private static void AssertFailed<T>(Result<T> result, Error expected)
        {
            Assert.True(result.IsFailure, "Expected " + expected.Code + " but it succeeded.");
            Assert.Equal(expected.Code, result.Error?.Code);
        }

        // A published-ready exam: scheduled, one room, one chief.
        private static async Task<(ExamSummary Exam, Domain.Modules.University.Room Room, Domain.Modules.People.Instructor Chief)> ReadyAsync(ExaminationTestEnv env, ExamSessionSummary session, UniCore.Application.Modules.Offerings.SectionInfo section, DateOnly day, int startHour = 9, int endHour = 11)
        {
            var exam = await env.CreateExamOkAsync(section, session);
            Assert.True((await env.ScheduleAsync(exam, day, startHour, endHour)).IsSuccess);
            var room = env.SeedRoom(40);
            await env.AddRoomOkAsync(exam, room);
            var chief = env.SeedInstructor();
            Assert.True((await env.AddInvigilatorAsync(exam, room, chief)).IsSuccess);

            return (exam, room, chief);
        }

        // ---------- sessions ----------

        [Fact]
        public async Task Session_dates_must_be_inside_the_exam_window_of_the_semester()
        {
            var env = new ExaminationTestEnv();
            env.AsRegistrar();

            var outside = await env.SessionHandler.CreateAsync(new CreateExamSessionCommand(env.Semester.Id, "Finals", ExamType.Final, new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 10)), Request);
            AssertFailed(outside, ExaminationErrors.SessionOutsideWindow);

            var midtermWindowForFinal = await env.SessionHandler.CreateAsync(new CreateExamSessionCommand(env.Semester.Id, "Finals", ExamType.Final, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 5)), Request);
            AssertFailed(midtermWindowForFinal, ExaminationErrors.SessionOutsideWindow);

            await env.CreateSessionOkAsync();
        }

        [Fact]
        public async Task Session_names_are_unique_per_semester_and_a_closed_semester_is_refused()
        {
            var env = new ExaminationTestEnv();
            await env.CreateSessionOkAsync();

            var again = await env.SessionHandler.CreateAsync(new CreateExamSessionCommand(env.Semester.Id, "Finals Fall 2026", ExamType.Final, new DateOnly(2027, 1, 8), new DateOnly(2027, 1, 12)), Request);
            AssertFailed(again, ExaminationErrors.SessionNameExists);

            var closed = env.SeedSemester(closed: true);
            var inClosed = await env.SessionHandler.CreateAsync(new CreateExamSessionCommand(closed.Id, "Finals", ExamType.Final, new DateOnly(2027, 1, 8), new DateOnly(2027, 1, 12)), Request);
            AssertFailed(inClosed, ExaminationErrors.SemesterClosed);
        }

        [Fact]
        public async Task Only_Manage_creates_a_session_and_a_student_cannot_read_them()
        {
            var env = new ExaminationTestEnv();
            env.AsStudent();

            var create = await env.SessionHandler.CreateAsync(new CreateExamSessionCommand(env.Semester.Id, "Finals", ExamType.Final, new DateOnly(2027, 1, 8), new DateOnly(2027, 1, 12)), Request);
            Assert.Equal(ErrorType.Forbidden, create.Error?.ErrorType);
            Assert.True((await env.SessionHandler.ListAsync(new(1, 10), null, null)).IsFailure);
            Assert.Empty(env.Sessions.Items);
        }

        [Fact]
        public async Task Closing_a_session_is_audited_and_stops_new_exams()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var section = env.SeedSection();

            env.AsRegistrar();
            Assert.True((await env.SessionHandler.CloseAsync(session.Id, Request)).IsSuccess);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == ExaminationAuditActions.SessionClosed);

            env.AsHead();
            AssertFailed(await env.Handler.CreateAsync(new CreateExamCommand(section.SectionId, session.Id, ExamType.Final, 100, 120), Request), ExaminationErrors.SessionClosed);
        }

        // ---------- exams ----------

        [Fact]
        public async Task Exam_is_created_by_the_department_of_the_section_only()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var section = env.SeedSection();

            env.AsHead(Guid.NewGuid());
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.CreateAsync(new CreateExamCommand(section.SectionId, session.Id, ExamType.Final, 100, 120), Request)).Error?.ErrorType);

            env.AsInstructor(section.SectionId);
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.CreateAsync(new CreateExamCommand(section.SectionId, session.Id, ExamType.Final, 100, 120), Request)).Error?.ErrorType);

            Assert.Empty(env.Exams.Items);
            await env.CreateExamOkAsync(section, session);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == ExaminationAuditActions.ExamCreated);
        }

        [Fact]
        public async Task One_live_exam_per_section_and_type_and_the_type_must_match_the_session()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var section = env.SeedSection();
            var exam = await env.CreateExamOkAsync(section, session);

            env.AsHead();
            AssertFailed(await env.Handler.CreateAsync(new CreateExamCommand(section.SectionId, session.Id, ExamType.Final, 100, 120), Request), ExaminationErrors.ExamExists);
            AssertFailed(await env.Handler.CreateAsync(new CreateExamCommand(env.SeedSection().SectionId, session.Id, ExamType.Midterm, 100, 120), Request), ExaminationErrors.TypeMismatch);

            // A cancelled exam frees the slot.
            Assert.True((await env.Handler.CancelAsync(exam.Id, Request)).IsSuccess);
            Assert.True((await env.Handler.CreateAsync(new CreateExamCommand(section.SectionId, session.Id, ExamType.Final, 100, 120), Request)).IsSuccess);
        }

        [Fact]
        public async Task Section_of_another_semester_is_refused()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var other = env.SeedSection(env.SeedSemester());

            env.AsHead();
            AssertFailed(await env.Handler.CreateAsync(new CreateExamCommand(other.SectionId, session.Id, ExamType.Final, 100, 120), Request), ExaminationErrors.SectionSemesterMismatch);
        }

        // ---------- schedule ----------

        [Fact]
        public async Task Date_must_be_inside_the_session()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync(start: new DateOnly(2027, 1, 8), end: new DateOnly(2027, 1, 12));
            var exam = await env.CreateExamOkAsync(env.SeedSection(), session);

            AssertFailed(await env.ScheduleAsync(exam, new DateOnly(2027, 1, 14)), ExaminationErrors.DateOutsideSession);
            Assert.True((await env.ScheduleAsync(exam, new DateOnly(2027, 1, 10))).IsSuccess);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == ExaminationAuditActions.ScheduleSet);
        }

        [Fact]
        public async Task Duration_must_fit_the_time_range()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var exam = await env.CreateExamOkAsync(env.SeedSection(), session, minutes: 180);

            AssertFailed(await env.ScheduleAsync(exam, Day, 9, 11), ExaminationErrors.DurationTooLong);
            Assert.True((await env.ScheduleAsync(exam, Day, 9, 12)).IsSuccess);
        }

        // ---------- conflicts ----------

        [Fact]
        public async Task A_student_cannot_have_two_overlapping_exams()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var first = env.SeedSection(students: 3);
            var second = env.SeedSection();
            env.Roster.Entries[second.SectionId].Add(env.Roster.Entries[first.SectionId][0]);

            var examA = await env.CreateExamOkAsync(first, session);
            Assert.True((await env.ScheduleAsync(examA, Day, 9, 11)).IsSuccess);
            var examB = await env.CreateExamOkAsync(second, session);

            AssertFailed(await env.ScheduleAsync(examB, Day, 10, 12), ExaminationErrors.StudentConflict);
            Assert.True((await env.ScheduleAsync(examB, Day, 11, 13)).IsSuccess);
        }

        [Fact]
        public async Task Different_students_may_sit_at_the_same_time()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var examA = await env.CreateExamOkAsync(env.SeedSection(students: 3), session);
            var examB = await env.CreateExamOkAsync(env.SeedSection(students: 3), session);

            Assert.True((await env.ScheduleAsync(examA, Day)).IsSuccess);
            Assert.True((await env.ScheduleAsync(examB, Day)).IsSuccess);
        }

        [Fact]
        public async Task A_room_cannot_host_two_overlapping_exams()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var (examA, room, _) = await ReadyAsync(env, session, env.SeedSection(students: 2), Day, 9, 11);
            var examB = await env.CreateExamOkAsync(env.SeedSection(students: 2), session);
            Assert.True((await env.ScheduleAsync(examB, Day, 10, 12)).IsSuccess);

            env.AsHead();
            AssertFailed(await env.Handler.AddRoomAsync(examB.Id, new AddExamRoomCommand(room.Id), Request), ExaminationErrors.RoomConflict);
            Assert.NotEqual(examA.Id, examB.Id);
        }

        [Fact]
        public async Task An_invigilator_cannot_watch_two_overlapping_exams()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var (_, _, chief) = await ReadyAsync(env, session, env.SeedSection(students: 2), Day, 9, 11);
            var examB = await env.CreateExamOkAsync(env.SeedSection(students: 2), session);
            Assert.True((await env.ScheduleAsync(examB, Day, 10, 12)).IsSuccess);
            var room = env.SeedRoom();
            await env.AddRoomOkAsync(examB, room);

            AssertFailed(await env.AddInvigilatorAsync(examB, room, chief), ExaminationErrors.InvigilatorConflict);
            // The failed call above already put the chief on the in-memory exam, so the next one is an assistant.
            Assert.True((await env.AddInvigilatorAsync(examB, room, env.SeedInstructor(), InvigilatorRole.Assistant)).IsSuccess);
        }

        // ---------- rooms and invigilators ----------

        [Fact]
        public async Task Room_and_invigilator_must_exist_and_be_active()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var exam = await env.CreateExamOkAsync(env.SeedSection(), session);
            var room = env.SeedRoom();
            await env.AddRoomOkAsync(exam, room);

            env.AsHead();
            AssertFailed(await env.Handler.AddRoomAsync(exam.Id, new AddExamRoomCommand(Guid.NewGuid()), Request), ExaminationErrors.RoomNotFound);
            AssertFailed(await env.Handler.AddRoomAsync(exam.Id, new AddExamRoomCommand(env.SeedRoom(active: false).Id), Request), ExaminationErrors.RoomInactive);
            AssertFailed(await env.Handler.AddInvigilatorAsync(exam.Id, new AddInvigilatorCommand(room.Id, Guid.NewGuid(), InvigilatorRole.Chief), Request), ExaminationErrors.InvigilatorNotFound);
            AssertFailed(await env.AddInvigilatorAsync(exam, room, env.SeedInstructor(active: false)), ExaminationErrors.InvigilatorInactive);
        }

        // ---------- publish ----------

        [Fact]
        public async Task Publish_needs_capacity_for_every_registered_student()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var section = env.SeedSection(students: 41);
            var (exam, _, _) = await ReadyAsync(env, session, section, Day);

            env.AsHead();
            AssertFailed(await env.Handler.PublishAsync(exam.Id, Request), ExaminationErrors.CapacityNotEnough);

            // A second room makes it enough.
            var room2 = env.SeedRoom(10);
            await env.AddRoomOkAsync(exam, room2);
            Assert.True((await env.AddInvigilatorAsync(exam, room2, env.SeedInstructor())).IsSuccess);
            Assert.True((await env.Handler.PublishAsync(exam.Id, Request)).IsSuccess);
        }

        [Fact]
        public async Task Publish_is_audited_raises_an_event_and_locks_changes()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var (exam, _, _) = await ReadyAsync(env, session, env.SeedSection(students: 5), Day);

            env.AsHead();
            var published = await env.Handler.PublishAsync(exam.Id, Request);
            Assert.True(published.IsSuccess, published.Error?.Code);
            Assert.Equal(ExamStatus.Published, published.Value!.Status);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == ExaminationAuditActions.ExamPublished);
            Assert.Contains(env.Exams.Items.Single(e => e.Id == exam.Id).DomainEvents, e => e is ExamPublished);

            AssertFailed(await env.Handler.UpdateAsync(exam.Id, new UpdateExamCommand(100, 90), Request), ExaminationErrors.ExamInvalidState);
            AssertFailed(await env.Handler.PublishAsync(exam.Id, Request), ExaminationErrors.ExamInvalidState);
        }

        [Fact]
        public async Task A_conflict_that_appears_later_stops_the_publish()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var shared = env.SeedSection(students: 2);
            var other = env.SeedSection();
            env.Roster.Entries[other.SectionId].Add(env.Roster.Entries[shared.SectionId][0]);

            var (exam, _, _) = await ReadyAsync(env, session, shared, Day, 9, 11);
            var examB = await env.CreateExamOkAsync(other, session);
            Assert.True((await env.ScheduleAsync(examB, Day, 12, 14)).IsSuccess);

            // Sneak an overlapping time in without the checker (a direct data change), then publish.
            var tracked = env.Exams.Items.Single(e => e.Id == examB.Id);
            ResultAssert.Succeeded(tracked.SetSchedule(Day, new TimeOnly(10, 0), new TimeOnly(12, 0), env.Clock.UtcNow));

            env.AsHead();
            AssertFailed(await env.Handler.PublishAsync(exam.Id, Request), ExaminationErrors.StudentConflict);
        }

        // ---------- read ----------

        [Fact]
        public async Task Roster_keeps_blocked_students_off_the_exam_list()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var section = env.SeedSection(students: 4);
            var exam = await env.CreateExamOkAsync(section, session);
            var blocked = env.Roster.Entries[section.SectionId][0].StudentId;
            env.Attendance.Blocked.Add(blocked);

            env.AsHead();
            var roster = await env.Handler.GetRosterAsync(exam.Id);

            Assert.True(roster.IsSuccess, roster.Error?.Code);
            Assert.Equal(4, roster.Value!.Registered);
            Assert.Equal([blocked], roster.Value.BlockedStudentIds);
            Assert.Equal(3, roster.Value.EligibleStudentIds.Count);
            Assert.DoesNotContain(blocked, roster.Value.EligibleStudentIds);
        }

        [Fact]
        public async Task An_instructor_reads_only_exams_of_their_own_sections()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var mine = env.SeedSection();
            var theirs = env.SeedSection();
            var myExam = await env.CreateExamOkAsync(mine, session);
            var otherExam = await env.CreateExamOkAsync(theirs, session);

            env.AsInstructor(mine.SectionId);
            Assert.True((await env.Handler.GetAsync(myExam.Id)).IsSuccess);
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.GetAsync(otherExam.Id)).Error?.ErrorType);
            Assert.Equal(ErrorType.Forbidden, (await env.Handler.CreateAsync(new CreateExamCommand(mine.SectionId, session.Id, ExamType.Final, 100, 90), Request)).Error?.ErrorType);
        }

        [Fact]
        public async Task A_closed_semester_blocks_exam_changes()
        {
            var env = new ExaminationTestEnv();
            var session = await env.CreateSessionOkAsync();
            var exam = await env.CreateExamOkAsync(env.SeedSection(), session);

            ResultAssert.Succeeded(env.Semester.AdvanceTo(UniCore.Domain.Modules.Calendar.SemesterStatus.RegistrationOpen, env.Clock.UtcNow));
            ResultAssert.Succeeded(env.Semester.AdvanceTo(UniCore.Domain.Modules.Calendar.SemesterStatus.InProgress, env.Clock.UtcNow));
            ResultAssert.Succeeded(env.Semester.AdvanceTo(UniCore.Domain.Modules.Calendar.SemesterStatus.Examinations, env.Clock.UtcNow));
            ResultAssert.Succeeded(env.Semester.AdvanceTo(UniCore.Domain.Modules.Calendar.SemesterStatus.GradingOpen, env.Clock.UtcNow));
            ResultAssert.Succeeded(env.Semester.AdvanceTo(UniCore.Domain.Modules.Calendar.SemesterStatus.Closed, env.Clock.UtcNow));

            AssertFailed(await env.ScheduleAsync(exam, Day), ExaminationErrors.SemesterClosed);
        }
    }
}
