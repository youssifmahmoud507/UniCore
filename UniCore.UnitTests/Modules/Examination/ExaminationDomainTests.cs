using UniCore.Domain.Modules.Examination;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Examination
{
    public class ExaminationDomainTests
    {
        private static readonly DateTimeOffset Now = new(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Day = new(2027, 1, 10);

        private static Exam NewExam(int minutes = 120)
            => ResultAssert.Succeeded(Exam.Create(Guid.NewGuid(), Guid.NewGuid(), ExamType.Final, 100, minutes, Now));

        private static (Exam Exam, Guid RoomId, Guid InstructorId) ReadyExam(int capacity = 30)
        {
            var exam = NewExam();
            ResultAssert.Succeeded(exam.SetSchedule(Day, new TimeOnly(9, 0), new TimeOnly(11, 0), Now));
            var roomId = Guid.NewGuid();
            ResultAssert.Succeeded(exam.AddRoom(roomId, capacity, Now));
            var instructorId = Guid.NewGuid();
            ResultAssert.Succeeded(exam.AddInvigilator(roomId, instructorId, InvigilatorRole.Chief, Now));

            return (exam, roomId, instructorId);
        }

        [Theory]
        [InlineData(0, 120)]
        [InlineData(1001, 120)]
        [InlineData(100, 14)]
        [InlineData(100, 481)]
        public void Create_rejects_invalid_score_or_duration(decimal score, int minutes)
            => ResultAssert.Failed(Exam.Create(Guid.NewGuid(), Guid.NewGuid(), ExamType.Final, score, minutes, Now), ExaminationErrors.ExamInvalid);

        [Fact]
        public void Schedule_must_hold_the_whole_duration_and_end_after_start()
        {
            var exam = NewExam(120);

            ResultAssert.Failed(exam.SetSchedule(Day, new TimeOnly(9, 0), new TimeOnly(10, 0), Now), ExaminationErrors.DurationTooLong);
            ResultAssert.Failed(exam.SetSchedule(Day, new TimeOnly(11, 0), new TimeOnly(9, 0), Now), ExaminationErrors.ScheduleInvalid);
            ResultAssert.Succeeded(exam.SetSchedule(Day, new TimeOnly(9, 0), new TimeOnly(11, 0), Now));
        }

        [Fact]
        public void Rescheduling_changes_the_same_schedule_row()
        {
            var exam = NewExam();
            ResultAssert.Succeeded(exam.SetSchedule(Day, new TimeOnly(9, 0), new TimeOnly(11, 0), Now));
            var first = exam.Schedule;

            ResultAssert.Succeeded(exam.SetSchedule(Day.AddDays(1), new TimeOnly(12, 0), new TimeOnly(14, 0), Now));

            Assert.Same(first, exam.Schedule);
            Assert.Equal(Day.AddDays(1), exam.Schedule!.ExamDate);
        }

        [Fact]
        public void Touching_times_do_not_overlap_but_crossing_times_do()
        {
            var a = NewExam();
            var b = NewExam();
            ResultAssert.Succeeded(a.SetSchedule(Day, new TimeOnly(9, 0), new TimeOnly(11, 0), Now));
            ResultAssert.Succeeded(b.SetSchedule(Day, new TimeOnly(11, 0), new TimeOnly(13, 0), Now));
            Assert.False(a.Schedule!.Overlaps(b.Schedule!));

            ResultAssert.Succeeded(b.SetSchedule(Day, new TimeOnly(10, 0), new TimeOnly(12, 0), Now));
            Assert.True(a.Schedule!.Overlaps(b.Schedule!));
        }

        [Fact]
        public void Update_must_still_fit_the_schedule()
        {
            var (exam, _, _) = ReadyExam();

            ResultAssert.Failed(exam.Update(100, 180, Now), ExaminationErrors.DurationTooLong);
            ResultAssert.Succeeded(exam.Update(80, 90, Now));
        }

        [Fact]
        public void A_room_cannot_be_added_twice_and_an_instructor_invigilates_once()
        {
            var (exam, roomId, instructorId) = ReadyExam();

            ResultAssert.Failed(exam.AddRoom(roomId, 30, Now), ExaminationErrors.RoomExists);
            ResultAssert.Failed(exam.AddInvigilator(roomId, instructorId, InvigilatorRole.Assistant, Now), ExaminationErrors.InvigilatorExists);
        }

        [Fact]
        public void A_room_has_one_chief()
        {
            var (exam, roomId, _) = ReadyExam();

            ResultAssert.Failed(exam.AddInvigilator(roomId, Guid.NewGuid(), InvigilatorRole.Chief, Now), ExaminationErrors.ChiefExists);
            ResultAssert.Succeeded(exam.AddInvigilator(roomId, Guid.NewGuid(), InvigilatorRole.Assistant, Now));
        }

        [Fact]
        public void An_invigilator_needs_a_room_of_the_exam()
            => ResultAssert.Failed(NewExam().AddInvigilator(Guid.NewGuid(), Guid.NewGuid(), InvigilatorRole.Chief, Now), ExaminationErrors.ExamRoomNotFound);

        [Fact]
        public void Removing_a_room_removes_its_invigilators()
        {
            var (exam, roomId, _) = ReadyExam();

            ResultAssert.Succeeded(exam.RemoveRoom(roomId, Now));

            Assert.Empty(exam.Rooms);
            Assert.Empty(exam.Invigilators);
        }

        [Fact]
        public void Publish_needs_schedule_rooms_capacity_and_a_chief_in_every_room()
        {
            var noSchedule = NewExam();
            ResultAssert.Failed(noSchedule.Publish(10, Now), ExaminationErrors.NotScheduled);

            var noRooms = NewExam();
            ResultAssert.Succeeded(noRooms.SetSchedule(Day, new TimeOnly(9, 0), new TimeOnly(11, 0), Now));
            ResultAssert.Failed(noRooms.Publish(10, Now), ExaminationErrors.NoRooms);

            var (small, _, _) = ReadyExam(capacity: 30);
            ResultAssert.Failed(small.Publish(31, Now), ExaminationErrors.CapacityNotEnough);

            var (noChief, room, _) = ReadyExam();
            ResultAssert.Succeeded(noChief.AddRoom(Guid.NewGuid(), 30, Now));
            ResultAssert.Failed(noChief.Publish(10, Now), ExaminationErrors.ChiefMissing);
        }

        [Fact]
        public void Publish_raises_an_event_and_locks_the_exam()
        {
            var (exam, roomId, instructorId) = ReadyExam();

            ResultAssert.Succeeded(exam.Publish(30, Now));

            Assert.Equal(ExamStatus.Published, exam.Status);
            Assert.Contains(exam.DomainEvents, e => e is ExamPublished);
            ResultAssert.Failed(exam.Publish(30, Now), ExaminationErrors.ExamInvalidState);
            ResultAssert.Failed(exam.SetSchedule(Day, new TimeOnly(9, 0), new TimeOnly(11, 0), Now), ExaminationErrors.ExamInvalidState);
            ResultAssert.Failed(exam.RemoveInvigilator(instructorId, Now), ExaminationErrors.ExamInvalidState);
            Assert.NotEqual(Guid.Empty, roomId);
        }

        [Fact]
        public void Cancel_raises_an_event_only_for_a_published_exam()
        {
            var draft = NewExam();
            ResultAssert.Succeeded(draft.Cancel(Now));
            Assert.DoesNotContain(draft.DomainEvents, e => e is ExamCancelled);
            ResultAssert.Failed(draft.Cancel(Now), ExaminationErrors.ExamInvalidState);

            var (published, _, _) = ReadyExam();
            ResultAssert.Succeeded(published.Publish(10, Now));
            ResultAssert.Succeeded(published.Cancel(Now));
            Assert.Contains(published.DomainEvents, e => e is ExamCancelled);
        }

        [Fact]
        public void Session_validates_and_closes_once()
        {
            ResultAssert.Failed(ExamSession.Create(Guid.NewGuid(), " ", ExamType.Final, Day, Day, Now), ExaminationErrors.SessionInvalid);
            ResultAssert.Failed(ExamSession.Create(Guid.NewGuid(), "Finals", ExamType.Final, Day, Day.AddDays(-1), Now), ExaminationErrors.SessionInvalid);

            var session = ResultAssert.Succeeded(ExamSession.Create(Guid.NewGuid(), " Finals ", ExamType.Final, Day, Day.AddDays(5), Now));
            Assert.Equal("Finals", session.Name);
            Assert.True(session.Contains(Day.AddDays(2)));
            Assert.False(session.Contains(Day.AddDays(6)));

            ResultAssert.Succeeded(session.Close(Now));
            ResultAssert.Failed(session.Close(Now), ExaminationErrors.SessionClosed);
        }
    }
}
