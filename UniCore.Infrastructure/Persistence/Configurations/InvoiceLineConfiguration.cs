using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
    {
        public void Configure(EntityTypeBuilder<InvoiceLine> builder)
        {
            builder.ToTable("InvoiceLines", "finance");

            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedNever();

            builder.Property(l => l.Description).IsRequired().HasMaxLength(InvoiceLine.MaxDescriptionLength);
            builder.Property(l => l.Quantity).IsRequired();
            builder.Property(l => l.UnitPrice).HasMoneyConversion();
            builder.Property(l => l.Amount).HasMoneyConversion();

            builder.HasOne<StudentCharge>().WithMany().HasForeignKey(l => l.StudentChargeId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            // A charge can be on only one invoice line (NULLs are ignored by the filtered index).
            builder.HasIndex(l => l.StudentChargeId).IsUnique().HasDatabaseName("UX_InvoiceLines_StudentCharge");
        }
    }


}
