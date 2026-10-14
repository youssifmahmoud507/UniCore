using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Infrastructure.Modules.Finance;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class InvoiceNumberCounterConfiguration : IEntityTypeConfiguration<InvoiceNumberCounter>
    {
        public void Configure(EntityTypeBuilder<InvoiceNumberCounter> builder)
        {
            builder.ToTable("InvoiceNumberCounters", "finance");

            builder.HasKey(c => c.CounterKey);
            builder.Property(c => c.CounterKey).IsRequired().HasMaxLength(10);
            builder.Property(c => c.LastValue).IsRequired();
        }
    }


}
