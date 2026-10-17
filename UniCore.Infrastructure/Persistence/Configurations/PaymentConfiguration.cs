using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments", "finance");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            builder.Property(p => p.Amount).HasMoneyConversion();
            builder.Property(p => p.RefundCommittedAmount).HasMoneyConversion();
            builder.Property(p => p.RefundedAmount).HasMoneyConversion();
            builder.Property(p => p.PaymentDate).IsRequired();
            builder.Property(p => p.Method).IsRequired();
            builder.Property(p => p.Reference).HasMaxLength(Payment.MaxReferenceLength);
            builder.Property(p => p.IdempotencyKey).IsRequired().HasMaxLength(Payment.MaxKeyLength);
            builder.Property(p => p.CreatedAt).IsRequired();

            // One payment per Idempotency-Key. This is what makes two parallel identical requests safe.
            builder.HasIndex(p => p.IdempotencyKey).IsUnique().HasDatabaseName("UX_Payments_IdempotencyKey");
            builder.HasIndex(p => p.InvoiceId);
        }
    }


}
