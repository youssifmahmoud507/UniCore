using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Students;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
    {
        public void Configure(EntityTypeBuilder<Refund> builder)
        {
            builder.ToTable("Refunds", "finance");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Amount).HasMoneyConversion();
            builder.Property(r => r.Reason).IsRequired().HasMaxLength(Refund.MaxReasonLength);
            builder.Property(r => r.RejectionReason).HasMaxLength(Refund.MaxRejectionReasonLength);
            builder.Property(r => r.Status).IsRequired();
            builder.Property(r => r.RequestedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(r => r.DomainEvents);

            builder.HasOne<Payment>().WithMany().HasForeignKey(r => r.PaymentId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Invoice>().WithMany().HasForeignKey(r => r.InvoiceId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Student>().WithMany().HasForeignKey(r => r.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(r => r.PaymentId);
            builder.HasIndex(r => r.InvoiceId);
            builder.HasIndex(r => new { r.StudentId, r.Status });
            builder.HasIndex(r => r.Status);
        }
    }



}
