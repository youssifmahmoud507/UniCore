using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.University.Configurations
{
    public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
    {
        public void Configure(EntityTypeBuilder<Department> builder)
        {
            builder.ToTable("Departments", "university");

            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();

            builder.Ignore(d => d.DomainEvents);

            builder.Property(d => d.Code).IsRequired().HasMaxLength(FieldRules.CodeMaxLength);
            builder.Property(d => d.Name).IsRequired().HasMaxLength(FieldRules.NameMaxLength);
            builder.Property(d => d.Description).HasMaxLength(FieldRules.DescriptionMaxLength);
            builder.Property(d => d.Email).HasMaxLength(FieldRules.EmailMaxLength);
            builder.Property(d => d.Phone).HasMaxLength(FieldRules.PhoneMaxLength);

            // UQ(FacultyId, Code): proposed in academic-lifecycle.md, open question 1.
            builder.HasIndex(d => new { d.FacultyId, d.Code }).IsUnique();

            builder.HasOne<Faculty>().WithMany().HasForeignKey(d => d.FacultyId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
