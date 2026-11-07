namespace UniCore.Application.Modules.Registrations
{
    // OverrideReason: only registration staff (Registration.Manage) may send it. It skips the registration-window
    // check (step 2) and is written to the audit log; every other check still applies.
    public sealed record RegisterCommand(Guid StudentId, Guid AcademicSemesterId, Guid CourseSectionId, string? OverrideReason);

    public sealed record DropCommand(IReadOnlyList<Guid> EnrollmentIds);
}
