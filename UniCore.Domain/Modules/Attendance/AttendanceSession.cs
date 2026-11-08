using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Attendance
{
    // An attendance session of one section on one date and start time (unique together). The instructor opens it,
    // marks the students while it is Open, then closes it; only closed sessions count in the percentage.
    // It is the aggregate root of its records, so two requests marking the same session cannot both win (RowVersion).
    public sealed class AttendanceSession : AggregateRoot
    {
        private readonly List<AttendanceRecord> _records = [];

        private AttendanceSession()
        {
        }

        public Guid CourseSectionId { get; private set; }
        public DateOnly SessionDate { get; private set; }
        public TimeOnly StartTime { get; private set; }
        public AttendanceSessionStatus Status { get; private set; }
        public Guid? OpenedByUserId { get; private set; }
        public DateTimeOffset OpenedAt { get; private set; }
        public DateTimeOffset? ClosedAt { get; private set; }

        public IReadOnlyList<AttendanceRecord> Records => _records.AsReadOnly();

        public bool IsOpen => Status == AttendanceSessionStatus.Open;

        public static Result<AttendanceSession> Open(Guid courseSectionId, DateOnly date, TimeOnly start, Guid? openedBy, DateTimeOffset now)
        {
            if (courseSectionId == Guid.Empty || date == default || start.Ticks % TimeSpan.TicksPerMinute != 0)
            {
                return AttendanceErrors.SessionInvalid;
            }

            return new AttendanceSession
            {
                Id = Guid.NewGuid(),
                CourseSectionId = courseSectionId,
                SessionDate = date,
                StartTime = start,
                Status = AttendanceSessionStatus.Open,
                OpenedByUserId = openedBy,
                OpenedAt = now
            };
        }

        // Marks (or re-marks) one student. Only while Open. A record whose absence was excused cannot be changed.
        public Result<AttendanceRecord> Mark(Guid studentId, Guid enrollmentId, AttendanceStatus status, int? minutesLate, Guid? markedBy, DateTimeOffset now)
        {
            if (!IsOpen)
            {
                return AttendanceErrors.SessionClosed;
            }

            var validLate = status == AttendanceStatus.Late ? minutesLate is >= 1 and <= 240 : minutesLate is null or 0;
            if (studentId == Guid.Empty || enrollmentId == Guid.Empty || !Enum.IsDefined(status) || status == AttendanceStatus.Excused || !validLate)
            {
                return AttendanceErrors.RecordInvalid;
            }

            var late = status == AttendanceStatus.Late ? minutesLate : null;
            var existing = _records.FirstOrDefault(r => r.StudentId == studentId);

            if (existing is null)
            {
                var created = AttendanceRecord.Create(Id, studentId, enrollmentId, status, late, markedBy, now);
                _records.Add(created);
                return created;
            }

            if (existing.Status == AttendanceStatus.Excused)
            {
                return AttendanceErrors.RecordExcused;
            }

            existing.Change(status, late, markedBy, now);
            return existing;
        }

        // Open -> Closed. The handler marks the students nobody marked as Absent first.
        public Result Close(DateTimeOffset now)
        {
            if (!IsOpen)
            {
                return Result.Fail(AttendanceErrors.SessionNotOpen);
            }

            Status = AttendanceSessionStatus.Closed;
            ClosedAt = now;
            return Result.Ok();
        }

        // An accepted excuse turns an absence into Excused (any session status).
        public Result ExcuseRecord(Guid recordId)
        {
            var record = _records.FirstOrDefault(r => r.Id == recordId);
            if (record is null)
            {
                return Result.Fail(AttendanceErrors.RecordNotFound);
            }

            if (record.Status != AttendanceStatus.Absent)
            {
                return Result.Fail(AttendanceErrors.ExcuseNotAbsence);
            }

            record.MarkExcused();
            return Result.Ok();
        }

        public AttendanceRecord? FindRecord(Guid recordId) => _records.FirstOrDefault(r => r.Id == recordId);
    }
}
