using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Transcripts
{
    // The numbers of a GPA calculation. Standing is null when there are no counted credits yet.
    public sealed record GpaFigures(decimal Gpa, int AttemptedCredits, int EarnedCredits, AcademicStanding? Standing);

    public sealed record StudentGpaInfo(Guid StudentId, GpaFigures Cumulative, int ReplacedAttempts);

    public sealed record SemesterGpaInfo(Guid StudentId, Guid SemesterId, GpaFigures Semester, GpaFigures CumulativeAtEnd);

    // What Graduation (Week 16), student services and reporting read: GPA, credits and academic standing, calculated from the
    // published grades (IGradeReadService) with the default policy. No permission check: callers are authorized by their own module.
    public interface IGpaCalculationService
    {
        // Cumulative GPA, attempted and earned credits and standing of a student. A student with no published grades gets 0 credits and a null standing.
        Task<Result<StudentGpaInfo>> GetCumulativeAsync(Guid studentId, CancellationToken cancellationToken = default);

        // One semester's GPA and the cumulative GPA at the end of it. TRANSCRIPT_NO_GRADES when the student has no published grade in it.
        Task<Result<SemesterGpaInfo>> GetSemesterAsync(Guid studentId, Guid semesterId, CancellationToken cancellationToken = default);
    }

    // What the official transcript workflow (Developer 2) calls AFTER its approval. It freezes the student's transcript as it is now.
    // The caller has already authorized the request; the issuer is the actor of the audit entry.
    public interface ITranscriptSnapshotService
    {
        Task<Result<TranscriptSummary>> IssueOfficialAsync(Guid studentId, Guid issuedByUserId, RequestInfo request, CancellationToken cancellationToken = default);
    }
}
