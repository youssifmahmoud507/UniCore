using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Students;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class StudentChargeConfiguration : IEntityTypeConfiguration<StudentCharge>
    {
        public void Configure(EntityTypeBuilder<StudentCharge> builder)
        {
            builder.ToTable("StudentCharges", "finance");

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();

            builder.Property(c => c.Description).IsRequired().HasMaxLength(StudentCharge.MaxDescriptionLength);
            builder.Property(c => c.ServiceReference).HasMaxLength(StudentCharge.MaxReferenceLength);
            builder.Property(c => c.Amount).HasMoneyConversion();
            builder.Property(c => c.DueDate).IsRequired();
            builder.Property(c => c.Status).IsRequired();
            builder.Property(c => c.CreatedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(c => c.DomainEvents);
            builder.Ignore(c => c.IsService);

            // AcademicSemesterId has no foreign key yet (Developer 1).
            builder.HasOne<Student>().WithMany().HasForeignKey(c => c.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<FeeDefinition>().WithMany().HasForeignKey(c => c.FeeDefinitionId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<FeeStructureItem>().WithMany().HasForeignKey(c => c.FeeStructureItemId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Invoice>().WithMany().HasForeignKey(c => c.InvoiceId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            // A student has at most one live semester charge per fee per semester: this is what makes generation idempotent.
            // Service charges have no semester, so they are outside this index.
            builder.HasIndex(c => new { c.StudentId, c.AcademicSemesterId, c.FeeDefinitionId })
                .IsUnique()
                .HasFilter("[Status] <> 'Cancelled' AND [AcademicSemesterId] IS NOT NULL")
                .HasDatabaseName("UX_StudentCharges_Live");

            // One live service charge per student per request: this is what makes IStudentChargeService idempotent.
            builder.HasIndex(c => new { c.StudentId, c.ServiceReference })
                .IsUnique()
                .HasFilter("[ServiceReference] IS NOT NULL AND [Status] <> 'Cancelled'")
                .HasDatabaseName("UX_StudentCharges_ServiceReference");

            builder.HasIndex(c => new { c.AcademicSemesterId, c.Status });
            builder.HasIndex(c => c.InvoiceId);
        }
    }

}
