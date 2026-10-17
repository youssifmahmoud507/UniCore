namespace UniCore.Domain.Modules.Finance
{
    public enum FinancialHoldReason
    {
        // Placed and released by the system, from overdue invoices.
        OverdueBalance = 0,

        // Placed by a person. Only a person releases it.
        Manual = 1
    }





}
