using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Graduation;

namespace UniCore.Infrastructure.Modules.Graduation.Configurations
{
    public sealed class GraduationApplicationConfiguration : IEntityTypeConfiguration<GraduationApplication>
    {
        public void Configure(EntityTypeBuilder<GraduationApplication> builder)
        {
            builder.ToTable("GraduationApplications", "graduation", table =>
            {
                table.HasCheckConstraint("CK_GraduationApplications_Figures", "([CumulativeGpa] IS NULL OR ([CumulativeGpa] >= 0 AND [CumulativeGpa] <= 5)) AND ([EarnedCredits] IS NULL OR [EarnedCredits] >= 0)");
            });

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();

            builder.Ignore(a => a.DomainEvents);
            builder.Ignore(a => a.IsActive);

            builder.Property(a => a.StudentId).IsRequired();
            builder.Property(a => a.AcademicProgramId).IsRequired();
            builder.Property(a => a.StudyPlanVersionId).IsRequired();

            // Status and Standing are stored as text by the convention in AppDbContext.
            builder.Property(a => a.Status).IsRequired();
            builder.Property(a => a.SubmittedAt).IsRequired();
            builder.Property(a => a.CumulativeGpa).HasPrecision(4, 2);
            builder.Property(a => a.DecisionNote).HasMaxLength(GraduationApplication.NoteMaxLength);

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            // One Submitted or Approved application per student: two requests at the same moment cannot both succeed.
            builder.HasIndex(a => a.StudentId).IsUnique().HasDatabaseName("UX_GraduationApplications_OneActivePerStudent")
                .HasFilter("[Status] IN ('Submitted', 'Approved')");
            builder.HasIndex(a => new { a.Status, a.SubmittedAt });
        }
    }
}
