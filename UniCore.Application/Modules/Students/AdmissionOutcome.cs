namespace UniCore.Application.Modules.Students
{
    // AccountProvisioned is false when the student was saved but the login account could not be created.
    public sealed record AdmissionOutcome(StudentDetail Student, bool AccountProvisioned, string? AccountErrorCode);
}
