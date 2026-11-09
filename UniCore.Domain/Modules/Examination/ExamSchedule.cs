using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Examination
{
    // The date and time of an exam (one per exam). Created and replaced only through Exam.
    public sealed class ExamSchedule : BaseEntity
    {
        private ExamSchedule()
        {
        }

        public Guid ExamId { get; private set; }
        public Guid ExamSessionId { get; private set; }
        public DateOnly ExamDate { get; private set; }
        public TimeOnly StartTime { get; private set; }
        public TimeOnly EndTime { get; private set; }

        internal static ExamSchedule Create(Guid examId, Guid sessionId, DateOnly date, TimeOnly start, TimeOnly end)
            => new() { Id = Guid.NewGuid(), ExamId = examId, ExamSessionId = sessionId, ExamDate = date, StartTime = start, EndTime = end };

        // Changed in place so EF updates the one row (a replaced row would clash with the unique index on ExamId).
        internal void Change(DateOnly date, TimeOnly start, TimeOnly end)
        {
            ExamDate = date;
            StartTime = start;
            EndTime = end;
        }

        // Same date and the two time ranges overlap (touching ranges do not).
        public bool Overlaps(ExamSchedule other) => ExamDate == other.ExamDate && StartTime < other.EndTime && other.StartTime < EndTime;
    }
}
