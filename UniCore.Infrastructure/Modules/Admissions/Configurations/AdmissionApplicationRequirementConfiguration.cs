using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.Admissions;

namespace UniCore.Infrastructure.Modules.Admissions.Configurations
{
    public sealed class AdmissionApplicationRequirementConfiguration : IEntityTypeConfiguration<AdmissionApplicationRequirement>
    {
        public void Configure(EntityTypeBuilder<AdmissionApplicationRequirement> builder)
        {
            builder.ToTable("AdmissionApplicationRequirements", "admissions");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Code).IsRequired().HasMaxLength(FieldRules.CodeMaxLength);
            builder.Property(r => r.Name).IsRequired().HasMaxLength(FieldRules.NameMaxLength);
            builder.Property(r => r.IsMandatory).IsRequired();
            builder.Property(r => r.ReviewStatus).IsRequired();
            builder.Property(r => r.ReviewNote).HasMaxLength(AdmissionDecision.MaxReasonLength);

            // The catalog entry is only referenced; it is never deleted (DeleteBehavior.Restrict).
            builder.HasOne<AdmissionRequirement>().WithMany().HasForeignKey(r => r.AdmissionRequirementId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            // A requirement appears once in an application.
            builder.HasIndex(r => new { r.AdmissionApplicationId, r.AdmissionRequirementId }).IsUnique();

            // DocumentId has no foreign key: the Documents module owns that table (a contract, not a join).
            builder.HasIndex(r => r.DocumentId);
        }
    }
}
