namespace UniCore.Api.Contracts.Admissions
{
    // Ids and flags are nullable so a missing value is reported as a validation error instead of silently becoming a default.
    public sealed record CreateApplicantRequest(string? FullName, string? Email, string? Phone, DateOnly? DateOfBirth);
    public sealed record UpdateApplicantRequest(string? FullName, string? Email, string? Phone, DateOnly? DateOfBirth);

    public sealed record CreateRequirementRequest(Guid? AcademicProgramId, string? Code, string? Name, string? Description, bool? IsMandatory);
    public sealed record UpdateRequirementRequest(string? Name, string? Description, bool? IsMandatory);

    public sealed record CreateApplicationRequest(Guid? ApplicantId, Guid? AcademicProgramId, Guid? AcademicYearId);
    public sealed record AttachDocumentRequest(Guid? DocumentId);
    public sealed record ReviewRequirementRequest(bool? Approve, string? Note);
    public sealed record DecisionRequest(string? Reason);
}
