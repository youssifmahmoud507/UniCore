using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.Curriculum;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.Curriculum.Configurations
{
    public sealed class StudyPlanConfiguration : IEntityTypeConfiguration<StudyPlan>
    {
        public void Configure(EntityTypeBuilder<StudyPlan> builder)
        {
            builder.ToTable("StudyPlans", "curriculum");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            builder.Ignore(p => p.DomainEvents);

            builder.Property(p => p.Code).IsRequired().HasMaxLength(FieldRules.CodeMaxLength);
            builder.Property(p => p.Name).IsRequired().HasMaxLength(FieldRules.NameMaxLength);
            builder.Property(p => p.Description).HasMaxLength(FieldRules.DescriptionMaxLength);
            builder.Property(p => p.IsActive).IsRequired();
            builder.Property(p => p.CreatedAt).IsRequired();

            builder.HasIndex(p => new { p.AcademicProgramId, p.Code }).IsUnique();

            builder.HasOne<AcademicProgram>().WithMany().HasForeignKey(p => p.AcademicProgramId).IsRequired().OnDelete(DeleteBehavior.Restrict);
        }
    }
}
