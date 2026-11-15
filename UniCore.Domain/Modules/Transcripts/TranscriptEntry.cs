using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Transcripts
{
    // What an official transcript is built from; the snapshot turns each into a TranscriptEntry.
    public sealed record TranscriptEntryData(
        Guid GradeId, Guid SemesterId, string SemesterLabel, DateOnly SemesterStart, Guid CourseId, string CourseCode, string CourseTitle, int Credits,
        decimal Percentage, string Letter, decimal GradePoints, bool IsPassing, bool IsCounted, decimal SemesterGpa, decimal CumulativeGpa);

    // One line of an official transcript, frozen at the moment it was issued (a later correction never changes it).
    public sealed class TranscriptEntry : BaseEntity
    {
        private TranscriptEntry()
        {
        }

        public Guid TranscriptSnapshotId { get; private set; }
        public int SequenceNo { get; private set; }
        public Guid GradeId { get; private set; }
        public Guid SemesterId { get; private set; }
        public string SemesterLabel { get; private set; } = string.Empty;
        public DateOnly SemesterStart { get; private set; }
        public Guid CourseId { get; private set; }
        public string CourseCode { get; private set; } = string.Empty;
        public string CourseTitle { get; private set; } = string.Empty;
        public int Credits { get; private set; }
        public decimal Percentage { get; private set; }
        public string Letter { get; private set; } = string.Empty;
        public decimal GradePoints { get; private set; }
        public bool IsPassing { get; private set; }

        // False when a repeated attempt replaced this one in the GPA.
        public bool IsCounted { get; private set; }

        // The GPA of this entry's semester and the cumulative GPA at the end of it.
        public decimal SemesterGpa { get; private set; }
        public decimal CumulativeGpa { get; private set; }

        internal static TranscriptEntry Create(Guid snapshotId, int sequenceNo, TranscriptEntryData d)
            => new()
            {
                Id = Guid.NewGuid(), TranscriptSnapshotId = snapshotId, SequenceNo = sequenceNo, GradeId = d.GradeId, SemesterId = d.SemesterId,
                SemesterLabel = d.SemesterLabel, SemesterStart = d.SemesterStart, CourseId = d.CourseId, CourseCode = d.CourseCode, CourseTitle = d.CourseTitle,
                Credits = d.Credits, Percentage = d.Percentage, Letter = d.Letter, GradePoints = d.GradePoints, IsPassing = d.IsPassing, IsCounted = d.IsCounted,
                SemesterGpa = d.SemesterGpa, CumulativeGpa = d.CumulativeGpa
            };
    }
}
