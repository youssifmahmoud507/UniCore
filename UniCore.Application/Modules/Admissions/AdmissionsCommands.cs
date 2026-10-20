namespace UniCore.Application.Modules.Admissions
{
    // Nullable fields: a missing value is reported by the domain as a validation error instead of becoming a default.
    public sealed record CreateApplicantCommand(string? FullName, string? Email, string? Phone, DateOnly? DateOfBirth);
    public sealed record UpdateApplicantCommand(string? FullName, string? Email, string? Phone, DateOnly? DateOfBirth);

    public sealed record CreateRequirementCommand(Guid AcademicProgramId, string? Code, string? Name, string? Description, bool IsMandatory);
    public sealed record UpdateRequirementCommand(string? Name, string? Description, bool IsMandatory);

    public sealed record CreateApplicationCommand(Guid ApplicantId, Guid AcademicProgramId, Guid AcademicYearId);
    public sealed record AttachDocumentCommand(Guid DocumentId);
    public sealed record ReviewRequirementCommand(bool? Approve, string? Note);
    public sealed record DecisionCommand(string? Reason);
}
