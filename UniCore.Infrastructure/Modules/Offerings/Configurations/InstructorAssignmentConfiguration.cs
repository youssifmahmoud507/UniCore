using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.People;

namespace UniCore.Infrastructure.Modules.Offerings.Configurations
{
    public sealed class InstructorAssignmentConfiguration : IEntityTypeConfiguration<InstructorAssignment>
    {
        public void Configure(EntityTypeBuilder<InstructorAssignment> builder)
        {
            builder.ToTable("InstructorAssignments", "offerings", table =>
            {
                table.HasCheckConstraint("CK_InstructorAssignments_EndedAt", "[EndedAt] IS NULL OR [EndedAt] >= [AssignedAt]");
            });

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();

            builder.Ignore(a => a.DomainEvents);
            builder.Ignore(a => a.IsActive);

            // Role is stored as text (max 50) by the convention in AppDbContext.
            builder.Property(a => a.Role).IsRequired();
            builder.Property(a => a.AssignedAt).IsRequired();

            builder.HasOne<CourseSection>().WithMany().HasForeignKey(a => a.CourseSectionId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Instructor>().WithMany().HasForeignKey(a => a.InstructorId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            // The two rules of the module, also enforced by the database: one active assignment per instructor
            // per section, and one active Primary per section. Keep in step with InstructorAssignmentHandler.
            builder.HasIndex(a => new { a.CourseSectionId, a.InstructorId }).IsUnique().HasDatabaseName("UX_InstructorAssignments_ActivePerInstructor").HasFilter("[EndedAt] IS NULL");
            builder.HasIndex(a => a.CourseSectionId).IsUnique().HasDatabaseName("UX_InstructorAssignments_OneActivePrimary").HasFilter("[Role] = 'Primary' AND [EndedAt] IS NULL");
            builder.HasIndex(a => a.InstructorId);
        }
    }
}
