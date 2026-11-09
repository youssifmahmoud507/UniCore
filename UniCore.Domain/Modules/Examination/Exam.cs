using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Examination
{
    // The exam of one section in one session. Aggregate root of its schedule (date and time), rooms and invigilators,
    // so one request changes it as a whole (RowVersion). Conflicts with OTHER exams (students, rooms, invigilators) and the
    // dates of the session and the semester are decided by the handler; the rules inside one exam are decided here.
    public sealed class Exam : AggregateRoot
    {
        public const int MinDurationMinutes = 15;
        public const int MaxDurationMinutes = 480;

        private readonly List<ExamRoom> _rooms = [];
        private readonly List<ExamInvigilator> _invigilators = [];

        private Exam()
        {
        }

        public Guid CourseSectionId { get; private set; }
        public Guid ExamSessionId { get; private set; }
        public ExamType ExamType { get; private set; }
        public decimal MaxScore { get; private set; }
        public int DurationMinutes { get; private set; }
        public ExamStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }
        public DateTimeOffset? PublishedAt { get; private set; }

        public ExamSchedule? Schedule { get; private set; }
        public IReadOnlyList<ExamRoom> Rooms => _rooms.AsReadOnly();
        public IReadOnlyList<ExamInvigilator> Invigilators => _invigilators.AsReadOnly();

        public bool IsDraft => Status == ExamStatus.Draft;
        public int TotalCapacity => _rooms.Sum(r => r.Capacity);

        public static Result<Exam> Create(Guid courseSectionId, Guid examSessionId, ExamType type, decimal maxScore, int durationMinutes, DateTimeOffset now)
        {
            if (courseSectionId == Guid.Empty || examSessionId == Guid.Empty || !Enum.IsDefined(type) || !AreValid(maxScore, durationMinutes))
            {
                return ExaminationErrors.ExamInvalid;
            }

            return new Exam
            {
                Id = Guid.NewGuid(),
                CourseSectionId = courseSectionId,
                ExamSessionId = examSessionId,
                ExamType = type,
                MaxScore = maxScore,
                DurationMinutes = durationMinutes,
                Status = ExamStatus.Draft,
                CreatedAt = now
            };
        }

        private static bool AreValid(decimal maxScore, int durationMinutes)
            => maxScore > 0 && maxScore <= 1000 && durationMinutes is >= MinDurationMinutes and <= MaxDurationMinutes;

        // Only a Draft changes. A new duration must still fit the time range that is already set.
        public Result Update(decimal maxScore, int durationMinutes, DateTimeOffset now)
        {
            if (!IsDraft)
            {
                return Result.Fail(ExaminationErrors.ExamInvalidState);
            }

            if (!AreValid(maxScore, durationMinutes))
            {
                return Result.Fail(ExaminationErrors.ExamInvalid);
            }

            if (Schedule is { } schedule && (schedule.EndTime - schedule.StartTime).TotalMinutes < durationMinutes)
            {
                return Result.Fail(ExaminationErrors.DurationTooLong);
            }

            MaxScore = maxScore;
            DurationMinutes = durationMinutes;
            UpdatedAt = now;
            return Result.Ok();
        }

        // Replaces the date and time. The time range must hold the whole duration.
        public Result SetSchedule(DateOnly date, TimeOnly start, TimeOnly end, DateTimeOffset now)
        {
            if (!IsDraft)
            {
                return Result.Fail(ExaminationErrors.ExamInvalidState);
            }

            if (date == default || start.Ticks % TimeSpan.TicksPerMinute != 0 || end.Ticks % TimeSpan.TicksPerMinute != 0 || end <= start)
            {
                return Result.Fail(ExaminationErrors.ScheduleInvalid);
            }

            if ((end - start).TotalMinutes < DurationMinutes)
            {
                return Result.Fail(ExaminationErrors.DurationTooLong);
            }

            if (Schedule is null)
            {
                Schedule = ExamSchedule.Create(Id, ExamSessionId, date, start, end);
            }
            else
            {
                Schedule.Change(date, start, end);
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        public Result<ExamRoom> AddRoom(Guid roomId, int roomCapacity, DateTimeOffset now)
        {
            if (!IsDraft)
            {
                return ExaminationErrors.ExamInvalidState;
            }

            if (roomId == Guid.Empty || roomCapacity <= 0)
            {
                return ExaminationErrors.RoomNotFound;
            }

            if (_rooms.Any(r => r.RoomId == roomId))
            {
                return ExaminationErrors.RoomExists;
            }

            var room = ExamRoom.Create(Id, roomId, roomCapacity);
            _rooms.Add(room);
            UpdatedAt = now;
            return room;
        }

        // Removing a room also removes its invigilators.
        public Result RemoveRoom(Guid roomId, DateTimeOffset now)
        {
            if (!IsDraft)
            {
                return Result.Fail(ExaminationErrors.ExamInvalidState);
            }

            var room = _rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room is null)
            {
                return Result.Fail(ExaminationErrors.ExamRoomNotFound);
            }

            _invigilators.RemoveAll(i => i.ExamRoomId == room.Id);
            _rooms.Remove(room);
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result<ExamInvigilator> AddInvigilator(Guid roomId, Guid instructorId, InvigilatorRole role, DateTimeOffset now)
        {
            if (!IsDraft)
            {
                return ExaminationErrors.ExamInvalidState;
            }

            if (roomId == Guid.Empty || instructorId == Guid.Empty || !Enum.IsDefined(role))
            {
                return ExaminationErrors.InvigilatorInvalid;
            }

            var room = _rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room is null)
            {
                return ExaminationErrors.ExamRoomNotFound;
            }

            if (_invigilators.Any(i => i.InstructorId == instructorId))
            {
                return ExaminationErrors.InvigilatorExists;
            }

            if (role == InvigilatorRole.Chief && _invigilators.Any(i => i.ExamRoomId == room.Id && i.Role == InvigilatorRole.Chief))
            {
                return ExaminationErrors.ChiefExists;
            }

            var invigilator = ExamInvigilator.Create(Id, room.Id, instructorId, role);
            _invigilators.Add(invigilator);
            UpdatedAt = now;
            return invigilator;
        }

        public Result RemoveInvigilator(Guid instructorId, DateTimeOffset now)
        {
            if (!IsDraft)
            {
                return Result.Fail(ExaminationErrors.ExamInvalidState);
            }

            var invigilator = _invigilators.FirstOrDefault(i => i.InstructorId == instructorId);
            if (invigilator is null)
            {
                return Result.Fail(ExaminationErrors.InvigilatorNotFound);
            }

            _invigilators.Remove(invigilator);
            UpdatedAt = now;
            return Result.Ok();
        }

        // Draft -> Published. Needs a date and time, enough seats for the registered students, and a chief in every room.
        // The handler has already checked the conflicts with other exams.
        public Result Publish(int registeredStudents, DateTimeOffset now)
        {
            if (!IsDraft)
            {
                return Result.Fail(ExaminationErrors.ExamInvalidState);
            }

            if (Schedule is null)
            {
                return Result.Fail(ExaminationErrors.NotScheduled);
            }

            if (_rooms.Count == 0)
            {
                return Result.Fail(ExaminationErrors.NoRooms);
            }

            if (TotalCapacity < registeredStudents)
            {
                return Result.Fail(ExaminationErrors.CapacityNotEnough);
            }

            if (_rooms.Any(r => !_invigilators.Any(i => i.ExamRoomId == r.Id && i.Role == InvigilatorRole.Chief)))
            {
                return Result.Fail(ExaminationErrors.ChiefMissing);
            }

            Status = ExamStatus.Published;
            PublishedAt = now;
            UpdatedAt = now;

            RaiseDomainEvent(new ExamPublished(Guid.NewGuid(), now, Id, CourseSectionId, ExamType, Schedule.ExamDate, Schedule.StartTime, Schedule.EndTime));
            return Result.Ok();
        }

        public Result Cancel(DateTimeOffset now)
        {
            if (Status == ExamStatus.Cancelled)
            {
                return Result.Fail(ExaminationErrors.ExamInvalidState);
            }

            var wasPublished = Status == ExamStatus.Published;

            Status = ExamStatus.Cancelled;
            UpdatedAt = now;

            if (wasPublished)
            {
                RaiseDomainEvent(new ExamCancelled(Guid.NewGuid(), now, Id, CourseSectionId, ExamType));
            }

            return Result.Ok();
        }
    }
}
