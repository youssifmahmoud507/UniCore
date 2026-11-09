using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Assessments;
using UniCore.Domain.Modules.Offerings;

namespace UniCore.Infrastructure.Modules.Assessments.Configurations
{
    public sealed class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
    {
        public void Configure(EntityTypeBuilder<Assessment> builder)
        {
            builder.ToTable("Assessments", "assessment");

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();

            builder.Ignore(a => a.DomainEvents);
            builder.Ignore(a => a.IsDraft);
            builder.Ignore(a => a.TotalWeight);

            // Status is stored as text (max 50) by the convention in AppDbContext.
            builder.Property(a => a.Status).IsRequired();
            builder.Property(a => a.CreatedAt).IsRequired();

            // Two requests changing the same scheme at once cannot both win.
            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasOne<CourseSection>().WithMany().HasForeignKey(a => a.CourseSectionId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(a => a.Components).WithOne().HasForeignKey(c => c.AssessmentId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(a => a.Components).UsePropertyAccessMode(PropertyAccessMode.Field);

            // One live (not cancelled) scheme per section.
            builder.HasIndex(a => a.CourseSectionId).IsUnique().HasDatabaseName("UX_Assessments_LivePerSection").HasFilter("[Status] <> 'Cancelled'");
        }
    }

    public sealed class AssessmentComponentConfiguration : IEntityTypeConfiguration<AssessmentComponent>
    {
        public void Configure(EntityTypeBuilder<AssessmentComponent> builder)
        {
            builder.ToTable("AssessmentComponents", "assessment", table =>
            {
                table.HasCheckConstraint("CK_AssessmentComponents_Weight", "[Weight] > 0 AND [Weight] <= 100");
                table.HasCheckConstraint("CK_AssessmentComponents_MaxScore", "[MaxScore] > 0 AND [MaxScore] <= 1000");
            });

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();

            builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
            builder.Property(c => c.Type).IsRequired();
            builder.Property(c => c.Weight).IsRequired().HasPrecision(5, 2);
            builder.Property(c => c.MaxScore).IsRequired().HasPrecision(7, 2);
            builder.Property(c => c.DueDate);

            // One component name per scheme; one midterm and one final at most.
            builder.HasIndex(c => new { c.AssessmentId, c.Name }).IsUnique();
            builder.HasIndex(c => new { c.AssessmentId, c.Type }).IsUnique().HasDatabaseName("UX_AssessmentComponents_OneExamPerType").HasFilter("[Type] IN ('Midterm', 'Final')");
        }
    }
}
