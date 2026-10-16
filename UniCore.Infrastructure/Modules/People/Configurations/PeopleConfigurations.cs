using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Common.Validation;
using UniCore.Domain.Modules.People;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.People.Configurations
{
    public sealed class PersonProfileConfiguration : IEntityTypeConfiguration<PersonProfile>
    {
        public void Configure(EntityTypeBuilder<PersonProfile> builder)
        {
            builder.ToTable("PersonProfiles", "people");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.Ignore(p => p.DomainEvents);
            builder.Ignore(p => p.FullName);

            builder.Property(p => p.FirstName).IsRequired().HasMaxLength(PersonProfile.MaxNameLength);
            builder.Property(p => p.LastName).IsRequired().HasMaxLength(PersonProfile.MaxNameLength);
            builder.Property(p => p.NationalId).HasMaxLength(30);
            builder.Property(p => p.Email).HasMaxLength(FieldRules.EmailMaxLength);
            builder.Property(p => p.Phone).HasMaxLength(FieldRules.PhoneMaxLength);

            // Unique when present: filtered indexes ignore the rows where the value is null.
            builder.HasIndex(p => p.NationalId).IsUnique().HasFilter("[NationalId] IS NOT NULL");
            builder.HasIndex(p => p.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
            builder.HasIndex(p => new { p.LastName, p.FirstName });

            // Students.PersonProfileId has no foreign key to this table yet (the Students migration predates it);
            // adding it needs a data check first, so it is a follow-up agreed with Developer 2.
        }
    }

    public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
    {
        public void Configure(EntityTypeBuilder<Employee> builder)
        {
            builder.ToTable("Employees", "people");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();
            builder.Ignore(e => e.DomainEvents);

            builder.Property(e => e.EmployeeNumber).IsRequired().HasMaxLength(FieldRules.CodeMaxLength);
            builder.Property(e => e.JobTitle).HasMaxLength(Employee.MaxJobTitleLength);

            builder.HasIndex(e => e.EmployeeNumber).IsUnique();
            builder.HasIndex(e => e.PersonProfileId).IsUnique();
            builder.HasIndex(e => e.DepartmentId);

            builder.HasOne<PersonProfile>().WithMany().HasForeignKey(e => e.PersonProfileId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class InstructorConfiguration : IEntityTypeConfiguration<Instructor>
    {
        public void Configure(EntityTypeBuilder<Instructor> builder)
        {
            builder.ToTable("Instructors", "people");

            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).ValueGeneratedNever();
            builder.Ignore(i => i.DomainEvents);

            builder.Property(i => i.Specialization).HasMaxLength(Instructor.MaxSpecializationLength);

            // One instructor per employee. The instructor has no DepartmentId: it comes from the employee.
            builder.HasIndex(i => i.EmployeeId).IsUnique();

            builder.HasOne<Employee>().WithMany().HasForeignKey(i => i.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class TeachingAssistantConfiguration : IEntityTypeConfiguration<TeachingAssistant>
    {
        public void Configure(EntityTypeBuilder<TeachingAssistant> builder)
        {
            builder.ToTable("TeachingAssistants", "people", table =>
            {
                table.HasCheckConstraint("CK_TeachingAssistants_EmployeeOrStudent", "[EmployeeId] IS NOT NULL OR [StudentId] IS NOT NULL");
            });

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();
            builder.Ignore(t => t.DomainEvents);

            builder.HasIndex(t => t.EmployeeId).IsUnique().HasFilter("[EmployeeId] IS NOT NULL");
            builder.HasIndex(t => t.StudentId).IsUnique().HasFilter("[StudentId] IS NOT NULL");

            builder.HasOne<Employee>().WithMany().HasForeignKey(t => t.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Student>().WithMany().HasForeignKey(t => t.StudentId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
