using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Examination
{
    // A room used by an exam. Capacity is the seats of the room when it was added (a later change of the room
    // does not change an exam that already counted its seats). Created and removed only through Exam.
    public sealed class ExamRoom : BaseEntity
    {
        private ExamRoom()
        {
        }

        public Guid ExamId { get; private set; }
        public Guid RoomId { get; private set; }
        public int Capacity { get; private set; }

        internal static ExamRoom Create(Guid examId, Guid roomId, int capacity)
            => new() { Id = Guid.NewGuid(), ExamId = examId, RoomId = roomId, Capacity = capacity };
    }
}
