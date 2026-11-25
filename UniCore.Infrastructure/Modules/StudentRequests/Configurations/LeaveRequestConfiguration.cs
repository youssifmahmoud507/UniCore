using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.StudentRequests;
using UniCore.Domain.Modules.Students;

namespace UniCore.Infrastructure.Modules.StudentRequests.Configurations
{
    public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
    {
        public void Configure(EntityTypeBuilder<LeaveRequest> builder)
        {
            builder.ToTable("LeaveRequests", "requests", table =>
            {
                table.HasCheckConstraint("CK_LeaveRequests_Dates", "[ExpectedReturnDate] > [StartDate]");
            });

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.StartDate).IsRequired();
            builder.Property(r => r.ExpectedReturnDate).IsRequired();
            builder.Property(r => r.Reason).IsRequired().HasMaxLength(StudentRequest.MaxReasonLength);
            builder.Property(r => r.Status).IsRequired();
            builder.Property(r => r.RequestedAt).IsRequired();
            builder.Property(r => r.FailureCode).HasMaxLength(StudentRequest.MaxFailureCodeLength);

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(r => r.DomainEvents);

            builder.HasOne<Student>().WithMany().HasForeignKey(r => r.StudentId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(r => r.RequestedByUserId);
            builder.HasIndex(r => r.WorkflowInstanceId);

            // One open leave request per student.
            builder.HasIndex(r => r.StudentId).IsUnique().HasDatabaseName("UX_LeaveRequests_PendingPerStudent").HasFilter("[Status] = 'Pending'");
        }
    }
}
