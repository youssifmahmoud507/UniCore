namespace UniCore.Domain.Modules.Finance
{
    // How a fee is charged. Fixed: once per student per semester (generated in bulk). PerCredit: the price is for one credit
    // hour, and every enrollment of the student is charged separately (credits x price) when the seat is taken. Stored as text.
    public enum FeeBasis
    {
        Fixed = 0,
        PerCredit = 1
    }
}
