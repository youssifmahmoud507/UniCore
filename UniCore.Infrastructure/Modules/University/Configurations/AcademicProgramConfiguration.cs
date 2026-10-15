using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.University.Configurations
{
    public sealed class AcademicProgramConfiguration : IEntityTypeConfiguration<AcademicProgram>
    {
        public void Configure(EntityTypeBuilder<AcademicProgram> builder)
        {
            // Database checks keep the numbers sane even if someone bypasses the domain.
            builder.ToTable("AcademicPrograms", "university", table =>
            {
                table.HasCheckConstraint("CK_AcademicPrograms_DurationYears", "[DurationYears] >= 1 AND [DurationYears] <= 10");
                table.HasCheckConstraint("CK_AcademicPrograms_TotalRequiredCredits", "[TotalRequiredCredits] >= 1 AND [TotalRequiredCredits] <= 400");
            });

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            builder.Ignore(p => p.DomainEvents);

            builder.Property(p => p.Code).IsRequired().HasMaxLength(FieldRules.CodeMaxLength);
            builder.Property(p => p.Name).IsRequired().HasMaxLength(FieldRules.NameMaxLength);

            // DegreeType is stored as text with max length 50 by the convention in AppDbContext.

            // UQ(DepartmentId, Code): proposed in academic-lifecycle.md, open question 1.
            builder.HasIndex(p => new { p.DepartmentId, p.Code }).IsUnique();

            builder.HasOne<Department>().WithMany().HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
