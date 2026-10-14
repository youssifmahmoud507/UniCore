using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class FeeDefinitionConfiguration : IEntityTypeConfiguration<FeeDefinition>
    {
        public void Configure(EntityTypeBuilder<FeeDefinition> builder)
        {
            builder.ToTable("FeeDefinitions", "finance");

            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();

            builder.Property(d => d.Code).IsRequired().HasMaxLength(FeeDefinition.MaxCodeLength);
            builder.Property(d => d.Name).IsRequired().HasMaxLength(FeeDefinition.MaxNameLength);
            builder.Property(d => d.Description).HasMaxLength(FeeDefinition.MaxDescriptionLength);
            builder.Property(d => d.DefaultAmount).HasMoneyConversion();
            builder.Property(d => d.IsActive).IsRequired();
            builder.Property(d => d.CreatedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(d => d.DomainEvents);

            // UniversityId has no foreign key yet: University belongs to Developer 1.
            builder.HasIndex(d => new { d.UniversityId, d.Code }).IsUnique();
            builder.HasIndex(d => d.IsActive);
        }
    }

}
