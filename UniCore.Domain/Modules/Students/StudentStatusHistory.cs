namespace UniCore.Domain.Modules.Students
{
    // Append-only audit trail of status changes.
    public sealed class StudentStatusHistory
    {
        private StudentStatusHistory()
        {
        }

        public Guid Id { get; private set; }
        public Guid StudentId { get; private set; }
        public StudentAcademicStatus OldStatus { get; private set; }
        public StudentAcademicStatus NewStatus { get; private set; }
        public DateOnly EffectiveDate { get; private set; }
        public string? Reason { get; private set; }
        public Guid ChangedByUserId { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }

        internal static StudentStatusHistory Record(Guid studentId, StudentAcademicStatus oldStatus, StudentAcademicStatus newStatus, DateOnly effectiveDate, string? reason, Guid changedByUserId, DateTimeOffset now)
            => new()
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                EffectiveDate = effectiveDate,
                Reason = reason,
                ChangedByUserId = changedByUserId,
                CreatedAt = now
            };
    }
}
