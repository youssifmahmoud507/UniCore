using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Transcripts;

namespace UniCore.Application.Modules.Transcripts
{
    // Freezes the transcript as it is now. Called by the official transcript workflow (Developer 2) after its approval, and by
    // TranscriptIssueHandler (registrar) after its permission check. Never checks permissions itself.
    public sealed class TranscriptSnapshotService(
        TranscriptAccess access, AcademicHistoryBuilder history, IAggregateRepository<TranscriptSnapshot> snapshots,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit) : ITranscriptSnapshotService
    {
        private readonly TranscriptAccess _access = access;
        private readonly AcademicHistoryBuilder _history = history;
        private readonly IAggregateRepository<TranscriptSnapshot> _snapshots = snapshots;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result<TranscriptSummary>> IssueOfficialAsync(Guid studentId, Guid issuedByUserId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await _access.LoadStudentAsync(studentId, cancellationToken);
            if (!loaded.TryGetValue(out var student))
            {
                return Result<TranscriptSummary>.Fail(loaded.Errors);
            }

            var built = await _history.BuildAsync(studentId, GpaPolicy.Default, cancellationToken);
            if (!built.TryGetValue(out var h))
            {
                return Result<TranscriptSummary>.Fail(built.Errors);
            }

            if (h.Semesters.Count == 0)
            {
                return TranscriptErrors.NoGrades;
            }

            var code = Guid.NewGuid().ToString("N")[..TranscriptSnapshot.VerificationCodeLength];
            var issued = TranscriptSnapshot.Issue(studentId, student.StudentNumber, code, issuedByUserId, h.Policy, h.Cumulative, TranscriptBuilder.ToEntries(h), _clock.UtcNow);
            if (!issued.TryGetValue(out var snapshot))
            {
                return Result<TranscriptSummary>.Fail(issued.Errors);
            }

            _snapshots.Add(snapshot);

            var entry = new AuditEntry(TranscriptAuditActions.Issued, issuedByUserId, "TranscriptSnapshot", snapshot.Id.ToString(), request.IpAddress, request.UserAgent,
                null, $"StudentId={studentId};Code={snapshot.VerificationCode};Gpa={snapshot.CumulativeGpa};Entries={snapshot.Entries.Count}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<TranscriptSummary>.Fail(committed.Errors) : TranscriptMapper.ToSummary(snapshot);
        }
    }
}
