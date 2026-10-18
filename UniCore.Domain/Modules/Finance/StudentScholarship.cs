using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // A scholarship held by one student for a period. While it is active and the invoice date falls inside the period,
    // every invoice of the student gets the scholarship automatically. Only one active award per (student, scholarship).
    public sealed class StudentScholarship : AggregateRoot
    {
        public const int MaxReasonLength = 200;

        private StudentScholarship()
        {
        }

        public Guid StudentId { get; private set; }
        public Guid ScholarshipId { get; private set; }
        public DateOnly ValidFrom { get; private set; }
        public DateOnly? ValidTo { get; private set; }
        public StudentScholarshipStatus Status { get; private set; }
        public string? Reason { get; private set; }
        public Guid? AwardedByUserId { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? RevokedAt { get; private set; }
        public Guid? RevokedByUserId { get; private set; }

        public static Result<StudentScholarship> Award(Guid studentId, Guid scholarshipId, DateOnly validFrom, DateOnly? validTo, string? reason, Guid? awardedByUserId, DateTimeOffset now)
        {
            if (studentId == Guid.Empty || scholarshipId == Guid.Empty || (validTo is { } to && to < validFrom))
            {
                return FinanceErrors.ScholarshipAwardInvalid;
            }

            var cleanReason = reason?.Trim();

            if (string.IsNullOrEmpty(cleanReason))
            {
                cleanReason = null;
            }
            else if (cleanReason.Length > MaxReasonLength)
            {
                return FinanceErrors.ScholarshipAwardInvalid;
            }

            return new StudentScholarship
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                ScholarshipId = scholarshipId,
                ValidFrom = validFrom,
                ValidTo = validTo,
                Status = StudentScholarshipStatus.Active,
                Reason = cleanReason,
                AwardedByUserId = awardedByUserId,
                CreatedAt = now
            };
        }

        public Result Revoke(Guid? revokedByUserId, DateTimeOffset now)
        {
            if (Status != StudentScholarshipStatus.Active)
            {
                return Result.Fail(FinanceErrors.ScholarshipAwardNotActive);
            }

            Status = StudentScholarshipStatus.Revoked;
            RevokedAt = now;
            RevokedByUserId = revokedByUserId;

            return Result.Ok();
        }

        public bool IsValidOn(DateOnly date)
            => Status == StudentScholarshipStatus.Active && ValidFrom <= date && (ValidTo is null || ValidTo >= date);
    }





}
