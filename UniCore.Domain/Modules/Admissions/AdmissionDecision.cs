using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Admissions
{
    // The record of a decision. Append-only: there is no method that changes it, and the application never
    // removes one. The reason is kept for the applicant file and for audit.
    public sealed class AdmissionDecision : BaseEntity
    {
        public const int MaxReasonLength = 1000;

        private AdmissionDecision()
        {
        }

        public Guid AdmissionApplicationId { get; private set; }
        public AdmissionDecisionType Type { get; private set; }
        public string? Reason { get; private set; }
        public Guid DecidedBy { get; private set; }
        public DateTimeOffset DecidedAt { get; private set; }

        // A reason is required unless the decision is a plain Accepted.
        internal static Result<AdmissionDecision> Create(Guid applicationId, AdmissionDecisionType type, string? reason, Guid decidedBy, DateTimeOffset now)
        {
            if (decidedBy == Guid.Empty)
            {
                return AdmissionsErrors.InvalidReference;
            }

            var cleanReason = reason?.Trim();
            if (string.IsNullOrEmpty(cleanReason))
            {
                cleanReason = null;
            }

            if (cleanReason is { Length: > MaxReasonLength })
            {
                return AdmissionsErrors.ReasonTooLong;
            }

            if (type != AdmissionDecisionType.Accepted && cleanReason is null)
            {
                return AdmissionsErrors.ReasonRequired;
            }

            return new AdmissionDecision
            {
                Id = Guid.NewGuid(),
                AdmissionApplicationId = applicationId,
                Type = type,
                Reason = cleanReason,
                DecidedBy = decidedBy,
                DecidedAt = now
            };
        }
    }
}
