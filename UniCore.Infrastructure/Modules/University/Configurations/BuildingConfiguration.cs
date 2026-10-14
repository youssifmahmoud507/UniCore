using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.University.Configurations
{
    public sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
    {
        public void Configure(EntityTypeBuilder<Building> builder)
        {
            builder.ToTable("Buildings", "university");

            builder.HasKey(b => b.Id);
            builder.Property(b => b.Id).ValueGeneratedNever();

            builder.Ignore(b => b.DomainEvents);

            builder.Property(b => b.Code).IsRequired().HasMaxLength(FieldRules.CodeMaxLength);
            builder.Property(b => b.Name).IsRequired().HasMaxLength(FieldRules.NameMaxLength);

            // UQ(CampusId, Code)
            builder.HasIndex(b => new { b.CampusId, b.Code }).IsUnique();

            builder.HasOne<Campus>().WithMany().HasForeignKey(b => b.CampusId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
