using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Curriculum;

namespace UniCore.Infrastructure.Modules.Curriculum.Configurations
{
    public sealed class StudyPlanCourseConfiguration : IEntityTypeConfiguration<StudyPlanCourse>
    {
        public void Configure(EntityTypeBuilder<StudyPlanCourse> builder)
        {
            builder.ToTable("StudyPlanCourses", "curriculum", table =>
            {
                table.HasCheckConstraint("CK_StudyPlanCourses_RequiredCredits", "[RequiredCredits] IS NULL OR ([RequiredCredits] >= 1 AND [RequiredCredits] <= 30)");
            });

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();

            // RequirementType is stored as text (max 50) by the convention in AppDbContext.
            builder.Property(c => c.RequirementType).IsRequired();

            builder.HasOne<AcademicLevel>().WithMany().HasForeignKey(c => c.AcademicLevelId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AcademicTerm>().WithMany().HasForeignKey(c => c.RecommendedTermId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            // CourseVersionId has no foreign key yet: CourseVersion arrives in Week 7, which adds the key in its migration.
            builder.HasIndex(c => new { c.StudyPlanVersionId, c.CourseVersionId }).IsUnique();
            builder.HasIndex(c => c.CourseVersionId);
        }
    }
}
