using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Curriculum;

namespace UniCore.Infrastructure.Modules.Curriculum.Configurations
{
    public sealed class CourseRequirementConfiguration : IEntityTypeConfiguration<CourseRequirement>
    {
        public void Configure(EntityTypeBuilder<CourseRequirement> builder)
        {
            builder.ToTable("CourseRequirements", "curriculum");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.RequirementType).IsRequired();

            // One graduation rule per course of a version. CourseVersionId is a real foreign key to CourseVersions.
            builder.HasOne<CourseVersion>().WithMany().HasForeignKey(r => r.CourseVersionId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(r => new { r.StudyPlanVersionId, r.CourseVersionId }).IsUnique();
        }
    }
}
