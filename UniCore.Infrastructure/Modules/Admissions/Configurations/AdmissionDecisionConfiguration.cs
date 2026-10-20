using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Admissions;

namespace UniCore.Infrastructure.Modules.Admissions.Configurations
{
    // Append-only table: the application code only ever inserts rows here.
    public sealed class AdmissionDecisionConfiguration : IEntityTypeConfiguration<AdmissionDecision>
    {
        public void Configure(EntityTypeBuilder<AdmissionDecision> builder)
        {
            builder.ToTable("AdmissionDecisions", "admissions");

            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();

            builder.Property(d => d.Type).IsRequired();
            builder.Property(d => d.Reason).HasMaxLength(AdmissionDecision.MaxReasonLength);
            builder.Property(d => d.DecidedBy).IsRequired();
            builder.Property(d => d.DecidedAt).IsRequired();

            builder.HasIndex(d => d.AdmissionApplicationId);
        }
    }
}
