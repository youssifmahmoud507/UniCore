using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;

// The namespace UniCore.Infrastructure.Modules.University hides the class name, so the entity gets an alias.
using UniversityEntity = UniCore.Domain.Modules.University.University;

namespace UniCore.Infrastructure.Modules.University.Configurations
{
    public sealed class UniversityConfiguration : IEntityTypeConfiguration<UniversityEntity>
    {
        public void Configure(EntityTypeBuilder<UniversityEntity> builder)
        {
            builder.ToTable("Universities", "university");

            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id).ValueGeneratedNever();

            // Domain events are not stored.
            builder.Ignore(u => u.DomainEvents);

            builder.Property(u => u.Code).IsRequired().HasMaxLength(FieldRules.CodeMaxLength);
            builder.Property(u => u.Name).IsRequired().HasMaxLength(FieldRules.NameMaxLength);
            builder.Property(u => u.Description).HasMaxLength(FieldRules.DescriptionMaxLength);
            builder.Property(u => u.Email).HasMaxLength(FieldRules.EmailMaxLength);
            builder.Property(u => u.Phone).HasMaxLength(FieldRules.PhoneMaxLength);
            builder.Property(u => u.Website).HasMaxLength(FieldRules.WebsiteMaxLength);

            // Both are unique in the data model. A violation is turned into DUPLICATE_RESOURCE by UnitOfWork.
            builder.HasIndex(u => u.Code).IsUnique();
            builder.HasIndex(u => u.Name).IsUnique();
        }
    }
}
