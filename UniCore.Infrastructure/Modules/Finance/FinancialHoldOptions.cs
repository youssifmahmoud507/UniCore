namespace UniCore.Infrastructure.Modules.Finance
{
    // appsettings: "Finance": { "Holds": { "GraceDays": 14, "MinimumOverdueBalance": 1.00, "WaiverDays": 7 } }
    public sealed class FinancialHoldOptions
    {
        public const string SectionName = "Finance:Holds";

        public int GraceDays { get; set; } = 14;

        public decimal MinimumOverdueBalance { get; set; } = 1m;

        public int WaiverDays { get; set; } = 7;

        public static bool IsValid(FinancialHoldOptions options) => options.GraceDays is >= 0 and <= 365 && options.WaiverDays is >= 0 and <= 90 && options.MinimumOverdueBalance is >= 0.01m and <= 1_000_000m && Math.Round(options.MinimumOverdueBalance, 2) == options.MinimumOverdueBalance;
    }



}
