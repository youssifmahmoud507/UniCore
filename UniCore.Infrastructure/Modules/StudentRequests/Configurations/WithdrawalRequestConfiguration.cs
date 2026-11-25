using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.StudentRequests;
using UniCore.Domain.Modules.Students;

namespace UniCore.Infrastructure.Modules.StudentRequests.Configurations
{
    public sealed class WithdrawalRequestConfiguration : IEntityTypeConfiguration<WithdrawalRequest>
    {
        public void Configure(EntityTypeBuilder<WithdrawalRequest> builder)
        {
            builder.ToTable("WithdrawalRequests", "requests");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Reason).IsRequired().HasMaxLength(StudentRequest.MaxReasonLength);
            builder.Property(r => r.Status).IsRequired();
            builder.Property(r => r.RequestedAt).IsRequired();
            builder.Property(r => r.FailureCode).HasMaxLength(StudentRequest.MaxFailureCodeLength);

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(r => r.DomainEvents);

            builder.HasOne<Student>().WithMany().HasForeignKey(r => r.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Registration>().WithMany().HasForeignKey(r => r.RegistrationId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(r => r.StudentId);
            builder.HasIndex(r => r.RequestedByUserId);
            builder.HasIndex(r => r.WorkflowInstanceId);

            // One open request per enrollment, also when two requests arrive at the same moment.
            builder.HasIndex(r => r.EnrollmentId).IsUnique().HasDatabaseName("UX_WithdrawalRequests_PendingPerEnrollment").HasFilter("[Status] = 'Pending'");
        }
    }
}
