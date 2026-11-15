using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Transcripts.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Transcripts;

namespace UniCore.Application.Modules.Transcripts
{
    // Reading GPA, academic history and transcripts (Transcripts.Read). A student reads their own; a head or dean the department's students;
    // the registrar everyone's. Order: permission alone -> student + scope -> calculation.
    public sealed class TranscriptQueryHandler(
        TranscriptAccess access, AcademicHistoryBuilder history, IAggregateRepository<TranscriptSnapshot> snapshots, IClock clock)
    {
        private readonly TranscriptAccess _access = access;
        private readonly AcademicHistoryBuilder _history = history;
        private readonly IAggregateRepository<TranscriptSnapshot> _snapshots = snapshots;
        private readonly IClock _clock = clock;

        public async Task<Result<StudentGpaInfo>> GetGpaAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var student = await _access.AuthorizeStudentAsync(Permissions.Transcripts.Read, studentId, cancellationToken);
            if (!student.TryGetValue(out _))
            {
                return Result<StudentGpaInfo>.Fail(student.Errors);
            }

            var built = await _history.BuildAsync(studentId, GpaPolicy.Default, cancellationToken);
            if (!built.TryGetValue(out var h))
            {
                return Result<StudentGpaInfo>.Fail(built.Errors);
            }

            return new StudentGpaInfo(studentId, TranscriptMapper.ToFigures(h.Cumulative), h.Cumulative.ReplacedAttempts);
        }

        public async Task<Result<SemesterGpaInfo>> GetSemesterGpaAsync(Guid studentId, Guid semesterId, CancellationToken cancellationToken = default)
        {
            var student = await _access.AuthorizeStudentAsync(Permissions.Transcripts.Read, studentId, cancellationToken);
            if (!student.TryGetValue(out _))
            {
                return Result<SemesterGpaInfo>.Fail(student.Errors);
            }

            var built = await _history.BuildAsync(studentId, GpaPolicy.Default, cancellationToken);
            if (!built.TryGetValue(out var h))
            {
                return Result<SemesterGpaInfo>.Fail(built.Errors);
            }

            var semester = h.Semesters.FirstOrDefault(s => s.SemesterId == semesterId);
            if (semester is null)
            {
                return TranscriptErrors.NoGrades;
            }

            return new SemesterGpaInfo(studentId, semesterId, TranscriptMapper.ToFigures(semester.Semester), TranscriptMapper.ToFigures(semester.Cumulative));
        }

        public async Task<Result<AcademicHistorySummary>> GetHistoryAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var student = await _access.AuthorizeStudentAsync(Permissions.Transcripts.Read, studentId, cancellationToken);
            if (!student.TryGetValue(out var found))
            {
                return Result<AcademicHistorySummary>.Fail(student.Errors);
            }

            var built = await _history.BuildAsync(studentId, GpaPolicy.Default, cancellationToken);
            return built.TryGetValue(out var h) ? TranscriptBuilder.ToHistory(studentId, found.StudentNumber, h) : Result<AcademicHistorySummary>.Fail(built.Errors);
        }

        // The unofficial transcript: calculated now, no verification code, nothing stored.
        public async Task<Result<TranscriptSummary>> GetTranscriptAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var student = await _access.AuthorizeStudentAsync(Permissions.Transcripts.Read, studentId, cancellationToken);
            if (!student.TryGetValue(out var found))
            {
                return Result<TranscriptSummary>.Fail(student.Errors);
            }

            var built = await _history.BuildAsync(studentId, GpaPolicy.Default, cancellationToken);
            return built.TryGetValue(out var h) ? TranscriptBuilder.ToUnofficial(studentId, found.StudentNumber, h, _clock.UtcNow) : Result<TranscriptSummary>.Fail(built.Errors);
        }

        public async Task<Result<IReadOnlyList<TranscriptSnapshotListItem>>> ListSnapshotsAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var student = await _access.AuthorizeStudentAsync(Permissions.Transcripts.Read, studentId, cancellationToken);
            if (!student.TryGetValue(out _))
            {
                return Result<IReadOnlyList<TranscriptSnapshotListItem>>.Fail(student.Errors);
            }

            var list = await _snapshots.ListAsync(new TranscriptSnapshotsOfStudentSpecification(studentId), cancellationToken);
            return Result<IReadOnlyList<TranscriptSnapshotListItem>>.Ok([.. list.Select(TranscriptMapper.ToListItem)]);
        }

        // One official transcript. Found first, then the student's scope; a snapshot of another student is "forbidden" like any other record.
        public async Task<Result<TranscriptSummary>> GetSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Transcripts.Read, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<TranscriptSummary>.Fail(permission.Errors);
            }

            var snapshot = await _snapshots.FirstOrDefaultAsync(new TranscriptSnapshotByIdSpecification(snapshotId), cancellationToken);
            if (snapshot is null)
            {
                return TranscriptErrors.SnapshotNotFound;
            }

            var student = await _access.AuthorizeStudentAsync(Permissions.Transcripts.Read, snapshot.StudentId, cancellationToken);
            return student.IsFailure ? Result<TranscriptSummary>.Fail(student.Errors) : TranscriptMapper.ToSummary(snapshot);
        }
    }

    // The registrar issues an official transcript directly (Transcripts.Issue); the student-requested flow is Developer 2's.
    public sealed class TranscriptIssueHandler(TranscriptAccess access, ITranscriptSnapshotService snapshots, ICurrentUser user)
    {
        private readonly TranscriptAccess _access = access;
        private readonly ITranscriptSnapshotService _snapshots = snapshots;
        private readonly ICurrentUser _user = user;

        public async Task<Result<TranscriptSummary>> IssueAsync(Guid studentId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var student = await _access.AuthorizeStudentAsync(Permissions.Transcripts.Issue, studentId, cancellationToken);
            if (!student.TryGetValue(out _))
            {
                return Result<TranscriptSummary>.Fail(student.Errors);
            }

            if (_user.UserId is not { } issuer)
            {
                return TranscriptErrors.ActorRequired;
            }

            return await _snapshots.IssueOfficialAsync(studentId, issuer, request, cancellationToken);
        }
    }
}
