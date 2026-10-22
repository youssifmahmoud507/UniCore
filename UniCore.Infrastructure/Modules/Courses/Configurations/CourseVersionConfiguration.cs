using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.Courses;

namespace UniCore.Infrastructure.Modules.Courses.Configurations
{
    public sealed class CourseVersionConfiguration : IEntityTypeConfiguration<CourseVersion>
    {
        public void Configure(EntityTypeBuilder<CourseVersion> builder)
        {
            builder.ToTable("CourseVersions", "courses", table =>
            {
                table.HasCheckConstraint("CK_CourseVersions_VersionNumber", "[VersionNumber] >= 1");
                table.HasCheckConstraint("CK_CourseVersions_Credits", "[Credits] >= 1 AND [Credits] <= 30");
                table.HasCheckConstraint("CK_CourseVersions_Hours", "[LectureHours] >= 0 AND [LectureHours] <= 60 AND [PracticalHours] >= 0 AND [PracticalHours] <= 60 AND [LectureHours] + [PracticalHours] > 0");
                table.HasCheckConstraint("CK_CourseVersions_EffectiveTo", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
            });

            builder.HasKey(v => v.Id);
            builder.Property(v => v.Id).ValueGeneratedNever();

            builder.Ignore(v => v.DomainEvents);

            builder.Property(v => v.Title).IsRequired().HasMaxLength(FieldRules.NameMaxLength);
            builder.Property(v => v.Description).HasMaxLength(FieldRules.DescriptionMaxLength);

            // Status is stored as text (max 50) by the convention in AppDbContext.
            builder.Property(v => v.Status).IsRequired();
            builder.Property(v => v.EffectiveFrom).IsRequired();
            builder.Property(v => v.CreatedAt).IsRequired();

            // Two people editing the same version: the second save fails with a concurrency conflict.
            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasOne<Course>().WithMany().HasForeignKey(v => v.CourseId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(v => new { v.CourseId, v.VersionNumber }).IsUnique();

            // At most one Active and one Draft version per course. Keep in step with the status rules in CourseVersion.
            builder.HasIndex(v => v.CourseId).IsUnique().HasDatabaseName("UX_CourseVersions_OneActivePerCourse").HasFilter("[Status] = 'Active'");
            builder.HasIndex(v => v.CourseId).IsUnique().HasDatabaseName("UX_CourseVersions_OneDraftPerCourse").HasFilter("[Status] = 'Draft'");
        }
    }
}
