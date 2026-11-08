using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Attendance
{
    // A warning given to a student in a section when the percentage falls to a worse level than the last warning.
    // The row is history: it is never changed. Raises AttendanceWarningIssued.
    public sealed class AttendanceWarning : AggregateRoot
    {
        private AttendanceWarning()
        {
        }

        public Guid StudentId { get; private set; }
        public Guid CourseSectionId { get; private set; }
        public decimal Percentage { get; private set; }
        public WarningLevel Level { get; private set; }
        public bool IsExamBlocking { get; private set; }
        public DateTimeOffset IssuedAt { get; private set; }

        public static Result<AttendanceWarning> Issue(Guid studentId, Guid courseSectionId, decimal percentage, WarningLevel level, bool isExamBlocking, DateTimeOffset now)
        {
            if (studentId == Guid.Empty || courseSectionId == Guid.Empty || percentage is < 0 or > 100 || !Enum.IsDefined(level))
            {
                return AttendanceErrors.RecordInvalid;
            }

            // Only the Critical level of an exam-blocking policy blocks.
            var blocking = isExamBlocking && level == WarningLevel.Critical;

            var warning = new AttendanceWarning
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                CourseSectionId = courseSectionId,
                Percentage = percentage,
                Level = level,
                IsExamBlocking = blocking,
                IssuedAt = now
            };

            warning.RaiseDomainEvent(new AttendanceWarningIssued(Guid.NewGuid(), now, warning.Id, studentId, courseSectionId, percentage, level, blocking));
            return warning;
        }
    }
}
