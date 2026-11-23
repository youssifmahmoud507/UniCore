using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Grading;

namespace UniCore.Infrastructure.Modules.Grading.Configurations
{
    public sealed class GradeChangeRequestConfiguration : IEntityTypeConfiguration<GradeChangeRequest>
    {
        public void Configure(EntityTypeBuilder<GradeChangeRequest> builder)
        {
            builder.ToTable("GradeChangeRequests", "grading", table =>
            {
                table.HasCheckConstraint("CK_GradeChangeRequests_Scores", "[OldScore] >= 0 AND [OldScore] <= 1000 AND [NewScore] >= 0 AND [NewScore] <= 1000");
            });

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.OldScore).IsRequired().HasPrecision(7, 2);
            builder.Property(r => r.NewScore).IsRequired().HasPrecision(7, 2);
            builder.Property(r => r.Reason).IsRequired().HasMaxLength(GradeChangeRequest.MaxReasonLength);
            builder.Property(r => r.Status).IsRequired();
            builder.Property(r => r.RequestedAt).IsRequired();
            builder.Property(r => r.FailureCode).HasMaxLength(GradeChangeRequest.MaxFailureCodeLength);

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(r => r.DomainEvents);

            builder.HasOne<Grade>().WithMany().HasForeignKey(r => r.GradeId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(r => r.GradeId);
            builder.HasIndex(r => r.WorkflowInstanceId);

            // One open request per grade, also when two requests arrive at the same moment.
            builder.HasIndex(r => r.GradeId).IsUnique().HasDatabaseName("UX_GradeChangeRequests_PendingPerGrade").HasFilter("[Status] = 'Pending'");
        }
    }
}
