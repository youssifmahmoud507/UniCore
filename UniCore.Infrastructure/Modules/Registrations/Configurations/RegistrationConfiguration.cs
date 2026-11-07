using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.Students;

namespace UniCore.Infrastructure.Modules.Registrations.Configurations
{
    public sealed class RegistrationConfiguration : IEntityTypeConfiguration<Registration>
    {
        public void Configure(EntityTypeBuilder<Registration> builder)
        {
            builder.ToTable("Registrations", "registration");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Ignore(r => r.DomainEvents);
            builder.Ignore(r => r.RegisteredEnrollments);
            builder.Ignore(r => r.RegisteredCredits);
            builder.Ignore(r => r.AcceptsChanges);

            // Stored as text (max 50) by the convention in AppDbContext.
            builder.Property(r => r.Status).IsRequired();
            builder.Property(r => r.CreatedAt).IsRequired();

            // Two requests changing the same registration at once: the second save fails with a concurrency conflict.
            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasOne<Student>().WithMany().HasForeignKey(r => r.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AcademicSemester>().WithMany().HasForeignKey(r => r.AcademicSemesterId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            // One registration per student and semester (also stops two parallel first registrations).
            builder.HasIndex(r => new { r.StudentId, r.AcademicSemesterId }).IsUnique();
            builder.HasIndex(r => new { r.AcademicSemesterId, r.Status });

            builder.HasMany(r => r.Enrollments).WithOne().HasForeignKey(e => e.RegistrationId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(r => r.Enrollments).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
