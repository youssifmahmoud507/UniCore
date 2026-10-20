using UniCore.Domain.Modules.Admissions;

namespace UniCore.Application.Modules.Admissions
{
    // Contains personal data: only returned to callers who hold Admissions.Read.
    public sealed record ApplicantSummary(Guid Id, string FullName, string Email, string? Phone, DateOnly DateOfBirth, Guid? UserId, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    public sealed record RequirementSummary(Guid Id, Guid AcademicProgramId, string Code, string Name, string? Description, bool IsMandatory, bool IsActive);

    public sealed record ApplicationSummary(Guid Id, Guid ApplicantId, Guid AcademicProgramId, Guid AcademicYearId, AdmissionApplicationStatus Status, DateTimeOffset Deadline, DateTimeOffset CreatedAt, DateTimeOffset? SubmittedAt, DateTimeOffset? DecidedAt);

    public sealed record ApplicationRequirementSummary(Guid Id, Guid AdmissionRequirementId, string Code, string Name, bool IsMandatory, Guid? DocumentId, RequirementReviewStatus ReviewStatus, string? ReviewNote, DateTimeOffset? ReviewedAt);

    public sealed record DecisionSummary(Guid Id, AdmissionDecisionType Type, string? Reason, Guid DecidedBy, DateTimeOffset DecidedAt);

    public sealed record ApplicationDetail(ApplicationSummary Application, IReadOnlyList<ApplicationRequirementSummary> Requirements, IReadOnlyList<DecisionSummary> Decisions);

    // Result of one run of the expiry job.
    public sealed record ExpiryOutcome(int Expired, int Failed);
}
