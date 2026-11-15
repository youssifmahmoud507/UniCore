using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Students;

namespace UniCore.Domain.Modules.Transcripts
{
    // An OFFICIAL transcript: everything frozen when it was issued, so it can be reproduced and checked later with its verification code,
    // whatever happens to the grades afterwards. It has no methods that change it.
    public sealed class TranscriptSnapshot : AggregateRoot
    {
        public const int VerificationCodeLength = 12;
        public const int StudentNumberMaxLength = 50;

        private readonly List<TranscriptEntry> _entries = [];

        private TranscriptSnapshot()
        {
        }

        public Guid StudentId { get; private set; }
        public string StudentNumber { get; private set; } = string.Empty;
        public string VerificationCode { get; private set; } = string.Empty;
        public Guid IssuedByUserId { get; private set; }
        public DateTimeOffset IssuedAt { get; private set; }

        public RepeatPolicy RepeatPolicy { get; private set; }
        public decimal CumulativeGpa { get; private set; }
        public int AttemptedCredits { get; private set; }
        public int EarnedCredits { get; private set; }
        public AcademicStanding? Standing { get; private set; }

        public IReadOnlyList<TranscriptEntry> Entries => _entries.AsReadOnly();

        public static Result<TranscriptSnapshot> Issue(
            Guid studentId, string? studentNumber, string? verificationCode, Guid issuedByUserId, GpaPolicy policy, GpaSummary cumulative,
            IReadOnlyCollection<TranscriptEntryData> entries, DateTimeOffset now)
        {
            var number = studentNumber?.Trim();
            var code = verificationCode?.Trim();

            if (studentId == Guid.Empty || issuedByUserId == Guid.Empty || string.IsNullOrWhiteSpace(number) || number.Length > StudentNumberMaxLength
                || string.IsNullOrWhiteSpace(code) || code.Length != VerificationCodeLength || entries is null || entries.Count == 0)
            {
                return TranscriptErrors.SnapshotInvalid;
            }

            var snapshot = new TranscriptSnapshot
            {
                Id = Guid.NewGuid(), StudentId = studentId, StudentNumber = number, VerificationCode = code.ToUpperInvariant(), IssuedByUserId = issuedByUserId, IssuedAt = now,
                RepeatPolicy = policy.Repeat, CumulativeGpa = cumulative.Gpa, AttemptedCredits = cumulative.AttemptedCredits, EarnedCredits = cumulative.EarnedCredits, Standing = cumulative.Standing
            };

            var sequence = 0;
            foreach (var entry in entries.OrderBy(e => e.SemesterStart).ThenBy(e => e.CourseCode, StringComparer.Ordinal))
            {
                snapshot._entries.Add(TranscriptEntry.Create(snapshot.Id, ++sequence, entry));
            }

            snapshot.RaiseDomainEvent(new TranscriptIssued(Guid.NewGuid(), now, snapshot.Id, studentId, snapshot.VerificationCode, snapshot.CumulativeGpa));
            return snapshot;
        }
    }
}
