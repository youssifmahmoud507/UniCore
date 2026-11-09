namespace UniCore.Domain.Modules.Grading
{
    // Draft (instructor enters) -> Submitted (instructor sends) -> Reviewed (head) -> Approved (dean) -> Published (registrar) -> Locked.
    // A Submitted or Reviewed grade can go back to Draft with a reason. Published and Locked grades change only through
    // an approved grade change (GradeChangeRequest, Developer 2) that calls IGradeCorrectionService.
    public enum GradeStatus
    {
        Draft,
        Submitted,
        Reviewed,
        Approved,
        Published,
        Locked
    }
}
