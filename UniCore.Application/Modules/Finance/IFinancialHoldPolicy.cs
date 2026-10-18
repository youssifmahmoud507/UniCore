namespace UniCore.Application.Modules.Finance
{
    // The numbers behind automatic holds. The Infrastructure fills them from configuration (Finance:Holds).
    public interface IFinancialHoldPolicy
    {
        // Days after the due date before an unpaid invoice counts as overdue.
        int GraceDays { get; }

        // The overdue total below which no hold is placed.
        decimal MinimumOverdueBalance { get; }

        // Days an automatic hold is not placed again after a person released it. 0 turns the waiver off.
        int WaiverDays { get; }
    }




}
