using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class FeeStructureItemConfiguration : IEntityTypeConfiguration<FeeStructureItem>
    {
        public void Configure(EntityTypeBuilder<FeeStructureItem> builder)
        {
            builder.ToTable("FeeStructureItems", "finance");

            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).ValueGeneratedNever();

            builder.Property(i => i.Amount).HasMoneyConversion();

            builder.HasOne<FeeDefinition>().WithMany().HasForeignKey(i => i.FeeDefinitionId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(i => new { i.FeeStructureId, i.FeeDefinitionId }).IsUnique();
        }
    }

}
