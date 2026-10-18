using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Students;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            builder.ToTable("Invoices", "finance");

            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).ValueGeneratedNever();

            builder.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(Invoice.MaxNumberLength);
            builder.Property(i => i.IssueDate).IsRequired();
            builder.Property(i => i.DueDate).IsRequired();
            builder.Property(i => i.Subtotal).HasMoneyConversion();
            builder.Property(i => i.DiscountAmount).HasMoneyConversion();
            builder.Property(i => i.TotalAmount).HasMoneyConversion();
            builder.Property(i => i.PaidAmount).HasMoneyConversion();
            builder.Property(i => i.Balance).HasMoneyConversion();
            builder.Property(i => i.Status).IsRequired();
            builder.Property(i => i.CreatedAt).IsRequired();


            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(i => i.DomainEvents);

            builder.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(i => i.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.HasMany(i => i.Payments).WithOne().HasForeignKey(p => p.InvoiceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(i => i.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(i => i.Reductions).WithOne().HasForeignKey(r => r.InvoiceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(i => i.Reductions).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasOne<Student>().WithMany().HasForeignKey(i => i.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(i => i.InvoiceNumber).IsUnique();
            builder.HasIndex(i => new { i.StudentId, i.Status });
            builder.HasIndex(i => i.IssueDate);



        }
    }


}
