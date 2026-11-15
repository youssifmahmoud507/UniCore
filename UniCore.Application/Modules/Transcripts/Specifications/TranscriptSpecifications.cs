using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Transcripts;

namespace UniCore.Application.Modules.Transcripts.Specifications
{
    public sealed class TranscriptSnapshotByIdSpecification : Specification<TranscriptSnapshot>
    {
        public TranscriptSnapshotByIdSpecification(Guid id)
        {
            SetCriteria(s => s.Id == id);
            AddInclude("Entries");
        }
    }

    // A student's official transcripts, newest first (no entries: the list shows only the header).
    public sealed class TranscriptSnapshotsOfStudentSpecification : Specification<TranscriptSnapshot>
    {
        public TranscriptSnapshotsOfStudentSpecification(Guid studentId)
        {
            SetCriteria(s => s.StudentId == studentId);
            ApplyOrderByDescending(s => s.IssuedAt);
        }
    }
}
