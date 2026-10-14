using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Infrastructure.Persistence
{
    public static class MoneyPropertyExtensions
    {
        // Money is stored as decimal(18,2).
        public static PropertyBuilder<Money> HasMoneyConversion(this PropertyBuilder<Money> builder) => builder.HasConversion(money => money.Amount, stored => Money.FromStored(stored)).HasPrecision(18, 2);
    }

}
