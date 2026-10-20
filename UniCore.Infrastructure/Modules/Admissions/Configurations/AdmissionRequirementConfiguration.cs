using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.Admissions;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.Admissions.Configurations
{
    public sealed class AdmissionRequirementConfiguration : IEntityTypeConfiguration<AdmissionRequirement>
    {
        public void Configure(EntityTypeBuilder<AdmissionRequirement> builder)
        {
            builder.ToTable("AdmissionRequirements", "admissions");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Ignore(r => r.DomainEvents);

            builder.Property(r => r.Code).IsRequired().HasMaxLength(FieldRules.CodeMaxLength);
            builder.Property(r => r.Name).IsRequired().HasMaxLength(FieldRules.NameMaxLength);
            builder.Property(r => r.Description).HasMaxLength(FieldRules.DescriptionMaxLength);
            builder.Property(r => r.IsMandatory).IsRequired();
            builder.Property(r => r.IsActive).IsRequired();
            builder.Property(r => r.CreatedAt).IsRequired();

            builder.HasIndex(r => new { r.AcademicProgramId, r.Code }).IsUnique();

            builder.HasOne<AcademicProgram>().WithMany().HasForeignKey(r => r.AcademicProgramId).IsRequired().OnDelete(DeleteBehavior.Restrict);
        }
    }
}
