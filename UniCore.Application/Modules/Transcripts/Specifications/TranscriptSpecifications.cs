using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Transcripts;

namespace UniCore.Application.Modules.Transcripts.Specifications
{
    // One query for all the semesters / years / terms a transcript needs, instead of one query per semester.
    public sealed class SemestersByIdsSpecification : Specification<UniCore.Domain.Modules.Calendar.AcademicSemester>
    {
        public SemestersByIdsSpecification(IReadOnlyCollection<Guid> ids) => SetCriteria(s => ids.Contains(s.Id));
    }

    public sealed class YearsByIdsSpecification : Specification<UniCore.Domain.Modules.Calendar.AcademicYear>
    {
        public YearsByIdsSpecification(IReadOnlyCollection<Guid> ids) => SetCriteria(y => ids.Contains(y.Id));
    }

    public sealed class TermsByIdsSpecification : Specification<UniCore.Domain.Modules.Calendar.AcademicTerm>
    {
        public TermsByIdsSpecification(IReadOnlyCollection<Guid> ids) => SetCriteria(t => ids.Contains(t.Id));
    }

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
