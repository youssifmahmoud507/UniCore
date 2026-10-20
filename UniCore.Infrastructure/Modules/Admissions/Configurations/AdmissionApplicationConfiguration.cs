using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Admissions;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.Admissions.Configurations
{
    public sealed class AdmissionApplicationConfiguration : IEntityTypeConfiguration<AdmissionApplication>
    {
        public void Configure(EntityTypeBuilder<AdmissionApplication> builder)
        {
            builder.ToTable("AdmissionApplications", "admissions");

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();

            builder.Ignore(a => a.DomainEvents);

            // Status is stored as text (max 50) by the convention in AppDbContext.
            builder.Property(a => a.Status).IsRequired();
            builder.Property(a => a.Deadline).IsRequired();
            builder.Property(a => a.CreatedAt).IsRequired();

            // Two reviewers working on the same application: the second save fails with a concurrency conflict.
            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasOne<Applicant>().WithMany().HasForeignKey(a => a.ApplicantId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AcademicProgram>().WithMany().HasForeignKey(a => a.AcademicProgramId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(a => a.Requirements).WithOne().HasForeignKey(r => r.AdmissionApplicationId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(a => a.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(a => a.Decisions).WithOne().HasForeignKey(d => d.AdmissionApplicationId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(a => a.Decisions).UsePropertyAccessMode(PropertyAccessMode.Field);

            // One application per (applicant, program, academic year) unless it ended without admission.
            // Keep the status list identical to AdmissionApplication.FinishedWithoutAdmission.
            builder.HasIndex(a => new
            {
                a.ApplicantId,
                a.AcademicProgramId,
                a.AcademicYearId
            }).IsUnique().HasDatabaseName("UX_AdmissionApplications_ActivePerApplicantProgramYear").HasFilter(
                                                                                                                    "[Status] <> 'Rejected' AND " +
                                                                                                                    "[Status] <> 'Withdrawn' AND " +
                                                                                                                    "[Status] <> 'Expired'");

            // The expiry job and the review queues.
            builder.HasIndex(a => new { a.Status, a.Deadline });
            builder.HasIndex(a => new { a.AcademicProgramId, a.Status });
        }
    }
}
