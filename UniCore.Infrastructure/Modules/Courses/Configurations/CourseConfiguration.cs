using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.Courses.Configurations
{
    public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
    {
        public void Configure(EntityTypeBuilder<Course> builder)
        {
            builder.ToTable("Courses", "courses");

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();

            builder.Ignore(c => c.DomainEvents);

            builder.Property(c => c.Code).IsRequired().HasMaxLength(FieldRules.CodeMaxLength);
            builder.Property(c => c.Title).IsRequired().HasMaxLength(FieldRules.NameMaxLength);
            builder.Property(c => c.Description).HasMaxLength(FieldRules.DescriptionMaxLength);

            // CourseType is stored as text (max 50) by the convention in AppDbContext.
            builder.Property(c => c.CourseType).IsRequired();
            builder.Property(c => c.IsActive).IsRequired();
            builder.Property(c => c.CreatedAt).IsRequired();

            builder.HasIndex(c => new { c.DepartmentId, c.Code }).IsUnique();

            builder.HasOne<Department>().WithMany().HasForeignKey(c => c.DepartmentId).IsRequired().OnDelete(DeleteBehavior.Restrict);
        }
    }
}
