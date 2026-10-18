namespace UniCore.Application.Modules.Finance
{
    public sealed record StudentFinanceDto(FinancialBalance Balance, IReadOnlyList<FinancialHoldDto> ActiveHolds, bool BlocksRegistration, bool BlocksGraduation, bool BlocksServices);




}
