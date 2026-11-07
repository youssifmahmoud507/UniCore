namespace UniCore.Domain.Modules.Registrations
{
    // Draft -> Submitted -> Completed. Draft or Submitted -> Cancelled. Stored as text.
    public enum RegistrationStatus
    {
        Draft,
        Submitted,
        Completed,
        Cancelled
    }
}
