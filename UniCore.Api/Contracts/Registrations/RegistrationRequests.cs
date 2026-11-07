namespace UniCore.Api.Contracts.Registrations
{
    // Ids are nullable so a missing value is reported as a validation error instead of silently becoming Guid.Empty.

    // OverrideReason: registration staff only; skips the registration window and is audited.
    public sealed record RegisterRequest(Guid? StudentId, Guid? AcademicSemesterId, Guid? CourseSectionId, string? OverrideReason);

    public sealed record DropRequest(IReadOnlyList<Guid>? EnrollmentIds);

    public sealed record WithdrawRequest(Guid? EnrollmentId);
}
