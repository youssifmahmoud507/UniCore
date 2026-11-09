using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Grading
{
    // The score of one assessment component. Created and changed only through Grade.
    public sealed class GradeComponentScore : BaseEntity
    {
        private GradeComponentScore()
        {
        }

        public Guid GradeId { get; private set; }

        // The component of the section's assessment (Assessments module). Not a foreign key: modules do not share tables.
        public Guid AssessmentComponentId { get; private set; }

        public decimal Score { get; private set; }

        internal static GradeComponentScore Create(Guid gradeId, Guid componentId, decimal score)
            => new() { Id = Guid.NewGuid(), GradeId = gradeId, AssessmentComponentId = componentId, Score = score };

        internal void Change(decimal score) => Score = score;
    }
}
