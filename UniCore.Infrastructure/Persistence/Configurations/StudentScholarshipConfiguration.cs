using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Students;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class StudentScholarshipConfiguration : IEntityTypeConfiguration<StudentScholarship>
    {
        public void Configure(EntityTypeBuilder<StudentScholarship> builder)
        {
            builder.ToTable("StudentScholarships", "finance");

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();

            builder.Property(a => a.ValidFrom).IsRequired();
            builder.Property(a => a.Status).IsRequired();
            builder.Property(a => a.Reason).HasMaxLength(StudentScholarship.MaxReasonLength);
            builder.Property(a => a.CreatedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(a => a.DomainEvents);

            builder.HasOne<Student>().WithMany().HasForeignKey(a => a.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Scholarship>().WithMany().HasForeignKey(a => a.ScholarshipId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            // One active award per (student, scholarship). Revoked ones are history and do not count.
            builder.HasIndex(a => new { a.StudentId, a.ScholarshipId }).IsUnique().HasFilter("[Status] = 'Active'").HasDatabaseName("UX_StudentScholarships_Active");

            builder.HasIndex(a => new { a.StudentId, a.Status });
        }
    }



}
