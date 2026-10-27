using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Offerings;

namespace UniCore.Infrastructure.Modules.Offerings.Configurations
{
    public sealed class CourseOfferingConfiguration : IEntityTypeConfiguration<CourseOffering>
    {
        public void Configure(EntityTypeBuilder<CourseOffering> builder)
        {
            builder.ToTable("CourseOfferings", "offerings", table =>
            {
                table.HasCheckConstraint("CK_CourseOfferings_MaxSections", "[MaxSections] IS NULL OR ([MaxSections] >= 1 AND [MaxSections] <= 50)");
            });

            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).ValueGeneratedNever();

            builder.Ignore(o => o.DomainEvents);
            builder.Ignore(o => o.AcceptsEnrollment);

            // Status is stored as text (max 50) by the convention in AppDbContext.
            builder.Property(o => o.Status).IsRequired();
            builder.Property(o => o.CreatedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasOne<CourseVersion>().WithMany().HasForeignKey(o => o.CourseVersionId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AcademicSemester>().WithMany().HasForeignKey(o => o.AcademicSemesterId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            // A course version is offered once per semester.
            builder.HasIndex(o => new { o.CourseVersionId, o.AcademicSemesterId }).IsUnique();
            builder.HasIndex(o => o.AcademicSemesterId);
        }
    }
}
