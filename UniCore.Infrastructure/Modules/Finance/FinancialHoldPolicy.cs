using Microsoft.Extensions.Options;
using UniCore.Application.Modules.Finance;

namespace UniCore.Infrastructure.Modules.Finance
{
    public sealed class FinancialHoldPolicy(IOptions<FinancialHoldOptions> options) : IFinancialHoldPolicy
    {
        private readonly FinancialHoldOptions _options = options.Value;

        public int GraceDays => _options.GraceDays;

        public decimal MinimumOverdueBalance => _options.MinimumOverdueBalance;

        public int WaiverDays => _options.WaiverDays;
    }



}
