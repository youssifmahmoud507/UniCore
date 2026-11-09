using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Assessments
{
    // One graded part of a section (quiz, midterm, final...). Created and changed only through Assessment.
    public sealed class AssessmentComponent : BaseEntity
    {
        private AssessmentComponent()
        {
        }

        public Guid AssessmentId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public ComponentType Type { get; private set; }

        // Percent of the final grade (0 < weight <= 100); the weights of one assessment total 100.
        public decimal Weight { get; private set; }

        // The score a student can get in this component.
        public decimal MaxScore { get; private set; }

        public DateOnly? DueDate { get; private set; }

        internal static AssessmentComponent Create(Guid assessmentId, string name, ComponentType type, decimal weight, decimal maxScore, DateOnly? dueDate)
            => new() { Id = Guid.NewGuid(), AssessmentId = assessmentId, Name = name, Type = type, Weight = weight, MaxScore = maxScore, DueDate = dueDate };

        internal void Change(string name, ComponentType type, decimal weight, decimal maxScore, DateOnly? dueDate)
        {
            Name = name;
            Type = type;
            Weight = weight;
            MaxScore = maxScore;
            DueDate = dueDate;
        }
    }
}
