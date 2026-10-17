using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class InvoiceReductionConfiguration : IEntityTypeConfiguration<InvoiceReduction>
    {
        public void Configure(EntityTypeBuilder<InvoiceReduction> builder)
        {
            builder.ToTable("InvoiceReductions", "finance");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Kind).IsRequired();
            builder.Property(r => r.Name).IsRequired().HasMaxLength(150);
            builder.Property(r => r.Type).IsRequired();
            builder.Property(r => r.Value).IsRequired();
            builder.Property(r => r.Amount).HasMoneyConversion();

            builder.HasIndex(r => r.InvoiceId);
        }
    }



}
