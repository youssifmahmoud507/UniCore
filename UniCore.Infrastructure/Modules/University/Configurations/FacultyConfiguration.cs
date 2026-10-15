using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.University;

using UniversityEntity = UniCore.Domain.Modules.University.University;

namespace UniCore.Infrastructure.Modules.University.Configurations
{
    public sealed class FacultyConfiguration : IEntityTypeConfiguration<Faculty>
    {
        public void Configure(EntityTypeBuilder<Faculty> builder)
        {
            builder.ToTable("Faculties", "university");

            builder.HasKey(f => f.Id);
            builder.Property(f => f.Id).ValueGeneratedNever();

            builder.Ignore(f => f.DomainEvents);

            builder.Property(f => f.Code).IsRequired().HasMaxLength(FieldRules.CodeMaxLength);
            builder.Property(f => f.Name).IsRequired().HasMaxLength(FieldRules.NameMaxLength);
            builder.Property(f => f.Description).HasMaxLength(FieldRules.DescriptionMaxLength);
            builder.Property(f => f.Email).HasMaxLength(FieldRules.EmailMaxLength);
            builder.Property(f => f.Phone).HasMaxLength(FieldRules.PhoneMaxLength);

            // UQ(UniversityId, Code): proposed in academic-lifecycle.md, open question 1.
            builder.HasIndex(f => new { f.UniversityId, f.Code }).IsUnique();

            builder.HasOne<UniversityEntity>().WithMany().HasForeignKey(f => f.UniversityId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
