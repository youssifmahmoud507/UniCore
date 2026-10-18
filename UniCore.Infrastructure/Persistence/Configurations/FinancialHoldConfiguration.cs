using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Students;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class FinancialHoldConfiguration : IEntityTypeConfiguration<FinancialHold>
    {
        public void Configure(EntityTypeBuilder<FinancialHold> builder)
        {
            builder.ToTable("FinancialHolds", "finance");

            builder.HasKey(h => h.Id);
            builder.Property(h => h.Id).ValueGeneratedNever();

            builder.Property(h => h.Reason).IsRequired();
            builder.Property(h => h.Areas).IsRequired();
            builder.Property(h => h.Status).IsRequired();
            builder.Property(h => h.OverdueAmount).HasMoneyConversion();
            builder.Property(h => h.Note).IsRequired().HasMaxLength(FinancialHold.MaxNoteLength);
            builder.Property(h => h.ReleaseNote).HasMaxLength(FinancialHold.MaxNoteLength);
            builder.Property(h => h.PlacedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(h => h.DomainEvents);
            builder.Ignore(h => h.IsAutomatic);

            builder.HasOne<Student>().WithMany().HasForeignKey(h => h.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(h => new { h.StudentId, h.Status });

            // One active automatic hold per student. Manual holds can be several.
            builder.HasIndex(h => h.StudentId).IsUnique().HasFilter("[Status] = 'Active' AND [Reason] = 'OverdueBalance'").HasDatabaseName("UX_FinancialHolds_ActiveAutomatic");
        }
    }


}
