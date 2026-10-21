using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

            // One graduation rule per course of a version. CourseVersionId: foreign key arrives with Week 7.
            builder.HasIndex(r => new { r.StudyPlanVersionId, r.CourseVersionId }).IsUnique();
        }
    }
}
