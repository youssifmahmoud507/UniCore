namespace UniCore.Domain.Modules.Graduation
{
    // Submitted -> Approved or Rejected (by the registrar), or Cancelled (by the student). Approved, Rejected and Cancelled are final.
    public enum GraduationApplicationStatus
    {
        Submitted,
        Approved,
        Rejected,
        Cancelled
    }
}
