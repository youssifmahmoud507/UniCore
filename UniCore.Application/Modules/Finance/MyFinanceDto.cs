namespace UniCore.Application.Modules.Finance
{
    // What a student sees of their money position: the balance and what is blocked (no staff notes).
    public sealed record MyFinanceDto(FinancialBalance Balance, int ActiveHolds, bool BlocksRegistration, bool BlocksGraduation, bool BlocksServices);


}
