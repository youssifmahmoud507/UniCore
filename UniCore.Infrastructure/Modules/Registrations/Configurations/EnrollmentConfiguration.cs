using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.Registrations;

namespace UniCore.Infrastructure.Modules.Registrations.Configurations
{
    public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
    {
        public void Configure(EntityTypeBuilder<Enrollment> builder)
        {
            builder.ToTable("Enrollments", "registration", table =>
            {
                table.HasCheckConstraint("CK_Enrollments_Credits", "[Credits] >= 1 AND [Credits] <= 30");
            });

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Ignore(e => e.HoldsSeat);

            builder.Property(e => e.Credits).IsRequired();
            builder.Property(e => e.Status).IsRequired();
            builder.Property(e => e.RegisteredAt).IsRequired();
            builder.Property(e => e.IdempotencyKey).HasMaxLength(100);

            builder.HasOne<CourseSection>().WithMany().HasForeignKey(e => e.CourseSectionId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<CourseVersion>().WithMany().HasForeignKey(e => e.CourseVersionId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            // A section appears once per registration (a dropped row is reused).
            builder.HasIndex(e => new { e.RegistrationId, e.CourseSectionId }).IsUnique();

            // A key is used once per registration: the database stops two parallel requests with the same key.
            builder.HasIndex(e => new { e.RegistrationId, e.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");

            // Roster queries and "who holds a seat of this section".
            builder.HasIndex(e => new { e.CourseSectionId, e.Status });
            builder.HasIndex(e => e.CourseId);
        }
    }
}
