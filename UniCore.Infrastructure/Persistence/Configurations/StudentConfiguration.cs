using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Students;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
    {
        public void Configure(EntityTypeBuilder<Student> builder)
        {
            builder.ToTable("Students", "students");

            // Keys are assigned by the domain, so EF must treat new rows as Added (never as Modified).
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Property(s => s.StudentNumber).IsRequired().HasMaxLength(30);
            builder.Property(s => s.AdmissionDate).IsRequired();
            builder.Property(s => s.CreatedAt).IsRequired();

            // Concurrency token: two parallel changes to the same student cannot both succeed.
            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(s => s.CurrentProgram);
            builder.Ignore(s => s.DomainEvents);

            builder.HasIndex(s => s.StudentNumber).IsUnique();
            builder.HasIndex(s => s.PersonProfileId).IsUnique();
            builder.HasIndex(s => s.UserId).IsUnique();
            builder.HasIndex(s => s.AcademicStatus);

            // PersonProfileId has no foreign key yet: PersonProfile belongs to Developer 1.
            builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(s => s.Profile).WithOne().HasForeignKey<StudentAcademicProfile>(p => p.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(s => s.ProgramHistory).WithOne().HasForeignKey(h => h.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(s => s.StatusHistory).WithOne().HasForeignKey(h => h.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(s => s.ProgramHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(s => s.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        }
    }

}
