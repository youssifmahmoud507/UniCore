using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Examination
{
    // An instructor invigilating one room of an exam. Created and removed only through Exam.
    public sealed class ExamInvigilator : BaseEntity
    {
        private ExamInvigilator()
        {
        }

        public Guid ExamId { get; private set; }
        public Guid ExamRoomId { get; private set; }
        public Guid InstructorId { get; private set; }
        public InvigilatorRole Role { get; private set; }

        internal static ExamInvigilator Create(Guid examId, Guid examRoomId, Guid instructorId, InvigilatorRole role)
            => new() { Id = Guid.NewGuid(), ExamId = examId, ExamRoomId = examRoomId, InstructorId = instructorId, Role = role };
    }
}
