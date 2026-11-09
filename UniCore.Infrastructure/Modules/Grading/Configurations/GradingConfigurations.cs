using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Assessments;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Offerings;

namespace UniCore.Infrastructure.Modules.Grading.Configurations
{
    public sealed class GradeScaleConfiguration : IEntityTypeConfiguration<GradeScale>
    {
        public void Configure(EntityTypeBuilder<GradeScale> builder)
        {
            builder.ToTable("GradeScales", "grading");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Ignore(s => s.DomainEvents);

            builder.Property(s => s.Name).IsRequired().HasMaxLength(GradeScale.NameMaxLength);
            builder.Property(s => s.IsDefault).IsRequired();
            builder.Property(s => s.CreatedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasMany(s => s.Bands).WithOne().HasForeignKey(b => b.GradeScaleId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(s => s.Bands).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(s => s.Name).IsUnique();

            // Only one default scale.
            builder.HasIndex(s => s.IsDefault).IsUnique().HasDatabaseName("UX_GradeScales_OneDefault").HasFilter("[IsDefault] = 1");
        }
    }

    public sealed class GradeScaleBandConfiguration : IEntityTypeConfiguration<GradeScaleBand>
    {
        public void Configure(EntityTypeBuilder<GradeScaleBand> builder)
        {
            builder.ToTable("GradeScaleBands", "grading", table =>
            {
                table.HasCheckConstraint("CK_GradeScaleBands_Min", "[MinPercentage] >= 0 AND [MinPercentage] <= 100");
                table.HasCheckConstraint("CK_GradeScaleBands_Points", "[GradePoints] >= 0 AND [GradePoints] <= 5");
            });

            builder.HasKey(b => b.Id);
            builder.Property(b => b.Id).ValueGeneratedNever();

            builder.Property(b => b.Letter).IsRequired().HasMaxLength(GradeScale.LetterMaxLength);
            builder.Property(b => b.MinPercentage).IsRequired().HasPrecision(5, 2);
            builder.Property(b => b.GradePoints).IsRequired().HasPrecision(3, 2);
            builder.Property(b => b.IsPassing).IsRequired();

            builder.HasIndex(b => new { b.GradeScaleId, b.Letter }).IsUnique();
            builder.HasIndex(b => new { b.GradeScaleId, b.MinPercentage }).IsUnique();
        }
    }

    public sealed class GradeConfiguration : IEntityTypeConfiguration<Grade>
    {
        public void Configure(EntityTypeBuilder<Grade> builder)
        {
            builder.ToTable("Grades", "grading", table => table.HasCheckConstraint("CK_Grades_Percentage", "[Percentage] IS NULL OR ([Percentage] >= 0 AND [Percentage] <= 100)"));

            builder.HasKey(g => g.Id);
            builder.Property(g => g.Id).ValueGeneratedNever();

            builder.Ignore(g => g.DomainEvents);
            builder.Ignore(g => g.IsFinal);

            // Status is stored as text by the convention in AppDbContext.
            builder.Property(g => g.Status).IsRequired();
            builder.Property(g => g.Credits).IsRequired();
            builder.Property(g => g.Percentage).HasPrecision(5, 2);
            builder.Property(g => g.Letter).HasMaxLength(GradeScale.LetterMaxLength);
            builder.Property(g => g.GradePoints).HasPrecision(3, 2);
            builder.Property(g => g.ReturnReason).HasMaxLength(Grade.ReasonMaxLength);
            builder.Property(g => g.CreatedAt).IsRequired();

            // Two requests changing the same grade at once cannot both win.
            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasOne<CourseSection>().WithMany().HasForeignKey(g => g.CourseSectionId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Assessment>().WithMany().HasForeignKey(g => g.AssessmentId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<GradeScale>().WithMany().HasForeignKey(g => g.GradeScaleId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(g => g.Scores).WithOne().HasForeignKey(s => s.GradeId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(g => g.Scores).UsePropertyAccessMode(PropertyAccessMode.Field);

            // One grade per enrollment. EnrollmentId and StudentId belong to the Registrations and Students modules: indexed, not foreign keys.
            builder.HasIndex(g => g.EnrollmentId).IsUnique();
            builder.HasIndex(g => new { g.CourseSectionId, g.Status });
            builder.HasIndex(g => new { g.StudentId, g.AcademicSemesterId });
        }
    }

    public sealed class GradeComponentScoreConfiguration : IEntityTypeConfiguration<GradeComponentScore>
    {
        public void Configure(EntityTypeBuilder<GradeComponentScore> builder)
        {
            builder.ToTable("GradeComponentScores", "grading", table => table.HasCheckConstraint("CK_GradeComponentScores_Score", "[Score] >= 0 AND [Score] <= 1000"));

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Property(s => s.Score).IsRequired().HasPrecision(7, 2);

            builder.HasIndex(s => new { s.GradeId, s.AssessmentComponentId }).IsUnique();
        }
    }
}
