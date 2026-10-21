using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Curriculum;

namespace UniCore.Infrastructure.Modules.Curriculum.Configurations
{
    public sealed class StudyPlanVersionConfiguration : IEntityTypeConfiguration<StudyPlanVersion>
    {
        public void Configure(EntityTypeBuilder<StudyPlanVersion> builder)
        {
            builder.ToTable("StudyPlanVersions", "curriculum", table =>
            {
                table.HasCheckConstraint("CK_StudyPlanVersions_VersionNumber", "[VersionNumber] >= 1");
                table.HasCheckConstraint("CK_StudyPlanVersions_EffectiveTo", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
            });

            builder.HasKey(v => v.Id);
            builder.Property(v => v.Id).ValueGeneratedNever();

            builder.Ignore(v => v.DomainEvents);

            // Status is stored as text (max 50) by the convention in AppDbContext.
            builder.Property(v => v.Status).IsRequired();
            builder.Property(v => v.EffectiveFrom).IsRequired();
            builder.Property(v => v.CreatedAt).IsRequired();

            // Two people editing the same version: the second save fails with a concurrency conflict.
            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasOne<StudyPlan>().WithMany().HasForeignKey(v => v.StudyPlanId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(v => v.Courses).WithOne().HasForeignKey(c => c.StudyPlanVersionId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(v => v.Courses).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(v => v.Requirements).WithOne().HasForeignKey(r => r.StudyPlanVersionId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(v => v.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(v => new { v.StudyPlanId, v.VersionNumber }).IsUnique();

            // At most one Active and one Draft version per plan. Keep in step with the status rules in StudyPlanVersion.
            builder.HasIndex(v => v.StudyPlanId).IsUnique().HasDatabaseName("UX_StudyPlanVersions_OneActivePerPlan").HasFilter("[Status] = 'Active'");
            builder.HasIndex(v => v.StudyPlanId).IsUnique().HasDatabaseName("UX_StudyPlanVersions_OneDraftPerPlan").HasFilter("[Status] = 'Draft'");
        }
    }
}
