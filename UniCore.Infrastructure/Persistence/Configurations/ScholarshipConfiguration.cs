using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class ScholarshipConfiguration : IEntityTypeConfiguration<Scholarship>
    {
        public void Configure(EntityTypeBuilder<Scholarship> builder)
        {
            builder.ToTable("Scholarships", "finance");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Property(s => s.Code).IsRequired().HasMaxLength(FeeDefinition.MaxCodeLength);
            builder.Property(s => s.Name).IsRequired().HasMaxLength(150);
            builder.Property(s => s.Description).HasMaxLength(500);
            builder.Property(s => s.Type).IsRequired();
            builder.Property(s => s.Value).IsRequired();
            builder.Property(s => s.IsActive).IsRequired();
            builder.Property(s => s.CreatedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(s => s.DomainEvents);

            builder.HasIndex(s => s.Code).IsUnique();
            builder.HasIndex(s => s.IsActive);
        }
    }



}
