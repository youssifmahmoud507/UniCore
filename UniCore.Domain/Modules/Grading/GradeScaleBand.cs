using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Grading
{
    // One letter of a scale: a percentage of at least MinPercentage earns Letter and GradePoints.
    public sealed class GradeScaleBand : BaseEntity
    {
        private GradeScaleBand()
        {
        }

        public Guid GradeScaleId { get; private set; }
        public string Letter { get; private set; } = string.Empty;
        public decimal MinPercentage { get; private set; }
        public decimal GradePoints { get; private set; }
        public bool IsPassing { get; private set; }

        internal static GradeScaleBand Create(Guid scaleId, string letter, decimal minPercentage, decimal gradePoints, bool isPassing)
            => new() { Id = Guid.NewGuid(), GradeScaleId = scaleId, Letter = letter, MinPercentage = minPercentage, GradePoints = gradePoints, IsPassing = isPassing };
    }
}
