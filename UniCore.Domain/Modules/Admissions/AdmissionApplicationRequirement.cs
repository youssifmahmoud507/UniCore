using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Admissions
{
    // A requirement as it stands inside one application: a copy of the catalog entry plus the document the
    // applicant linked and what the reviewer decided. Only AdmissionApplication creates and changes it.
    public sealed class AdmissionApplicationRequirement : BaseEntity
    {
        private AdmissionApplicationRequirement()
        {
        }

        public Guid AdmissionApplicationId { get; private set; }
        public Guid AdmissionRequirementId { get; private set; }

        // Copied from the catalog when the application is created.
        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public bool IsMandatory { get; private set; }

        // The uploaded file lives in the Documents module; here we keep only its id.
        public Guid? DocumentId { get; private set; }
        public DateTimeOffset? DocumentAttachedAt { get; private set; }

        public RequirementReviewStatus ReviewStatus { get; private set; }
        public string? ReviewNote { get; private set; }
        public Guid? ReviewedBy { get; private set; }
        public DateTimeOffset? ReviewedAt { get; private set; }

        internal bool HasDocument => DocumentId is not null;

        internal static AdmissionApplicationRequirement CopyOf(Guid applicationId, AdmissionRequirement requirement) => new()
        {
            Id = Guid.NewGuid(),
            AdmissionApplicationId = applicationId,
            AdmissionRequirementId = requirement.Id,
            Code = requirement.Code,
            Name = requirement.Name,
            IsMandatory = requirement.IsMandatory,
            ReviewStatus = RequirementReviewStatus.Pending
        };

        // A new (or replaced) document always goes back to Pending review.
        internal Result AttachDocument(Guid documentId, DateTimeOffset now)
        {
            if (documentId == Guid.Empty)
            {
                return Result.Fail(AdmissionsErrors.DocumentInvalid);
            }

            DocumentId = documentId;
            DocumentAttachedAt = now;
            ReviewStatus = RequirementReviewStatus.Pending;
            ReviewNote = null;
            ReviewedBy = null;
            ReviewedAt = null;
            return Result.Ok();
        }

        internal Result Review(bool approve, string? note, Guid reviewerId, DateTimeOffset now)
        {
            if (!HasDocument)
            {
                return Result.Fail(AdmissionsErrors.RequirementNotReviewable);
            }

            var cleanNote = note?.Trim();
            if (string.IsNullOrEmpty(cleanNote))
            {
                cleanNote = null;
            }

            if (cleanNote is { Length: > AdmissionDecision.MaxReasonLength })
            {
                return Result.Fail(AdmissionsErrors.ReasonTooLong);
            }

            if (!approve && cleanNote is null)
            {
                return Result.Fail(AdmissionsErrors.ReviewNoteRequired);
            }

            ReviewStatus = approve ? RequirementReviewStatus.Approved : RequirementReviewStatus.Rejected;
            ReviewNote = cleanNote;
            ReviewedBy = reviewerId;
            ReviewedAt = now;
            return Result.Ok();
        }
    }
}
