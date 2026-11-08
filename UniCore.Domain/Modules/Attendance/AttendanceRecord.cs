using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Attendance
{
    // One student in one session (unique on session + student). Created and changed only through AttendanceSession.
    public sealed class AttendanceRecord : BaseEntity
    {
        private AttendanceRecord()
        {
        }

        public Guid AttendanceSessionId { get; private set; }
        public Guid StudentId { get; private set; }
        public Guid EnrollmentId { get; private set; }
        public AttendanceStatus Status { get; private set; }
        public int? MinutesLate { get; private set; }
        public Guid? MarkedByUserId { get; private set; }
        public DateTimeOffset MarkedAt { get; private set; }

        internal static AttendanceRecord Create(Guid sessionId, Guid studentId, Guid enrollmentId, AttendanceStatus status, int? minutesLate, Guid? markedBy, DateTimeOffset now)
            => new()
            {
                Id = Guid.NewGuid(),
                AttendanceSessionId = sessionId,
                StudentId = studentId,
                EnrollmentId = enrollmentId,
                Status = status,
                MinutesLate = minutesLate,
                MarkedByUserId = markedBy,
                MarkedAt = now
            };

        internal void Change(AttendanceStatus status, int? minutesLate, Guid? markedBy, DateTimeOffset now)
        {
            Status = status;
            MinutesLate = minutesLate;
            MarkedByUserId = markedBy;
            MarkedAt = now;
        }

        internal void MarkExcused() => Status = AttendanceStatus.Excused;
    }
}
