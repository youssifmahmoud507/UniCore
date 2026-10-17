namespace UniCore.Domain.Modules.Finance
{
    // What a hold blocks. A hold can block one or several of these.
    [Flags]
    public enum FinancialHoldArea
    {
        None = 0,
        Registration = 1,
        Graduation = 2,
        Services = 4,
        All = Registration | Graduation | Services
    }





}
