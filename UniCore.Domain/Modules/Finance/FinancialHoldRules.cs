namespace UniCore.Domain.Modules.Finance
{
    public static class FinancialHoldRules
    {
        // An invoice is overdue when its due date plus the grace days is before today:
        // that is, when the due date is before this cutoff.
        public static DateOnly OverdueCutoff(DateOnly today, int graceDays) => today.AddDays(-graceDays);

        public static bool IsOverdue(DateOnly dueDate, DateOnly today, int graceDays) => dueDate < OverdueCutoff(today, graceDays);

        // A student is held when what is overdue is more than zero and at least the minimum.
        public static bool ShouldHold(Money overdue, Money minimum) => overdue.IsPositive && overdue >= minimum;
    }





}
