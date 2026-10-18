using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class DiscountConfiguration : IEntityTypeConfiguration<Discount>
    {
        public void Configure(EntityTypeBuilder<Discount> builder)
        {
            builder.ToTable("Discounts", "finance");

            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();

            builder.Property(d => d.Code).IsRequired().HasMaxLength(FeeDefinition.MaxCodeLength);
            builder.Property(d => d.Name).IsRequired().HasMaxLength(150);
            builder.Property(d => d.Description).HasMaxLength(500);
            builder.Property(d => d.Type).IsRequired();
            builder.Property(d => d.Value).IsRequired();
            builder.Property(d => d.IsActive).IsRequired();
            builder.Property(d => d.CreatedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(d => d.DomainEvents);

            builder.HasIndex(d => d.Code).IsUnique();
            builder.HasIndex(d => d.IsActive);
        }
    }



}
