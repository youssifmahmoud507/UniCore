using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class WorkflowApprovalConfiguration : IEntityTypeConfiguration<WorkflowApproval>
    {
        public void Configure(EntityTypeBuilder<WorkflowApproval> builder)
        {
            builder.ToTable("WorkflowApprovals", "workflow");

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();

            builder.Property(a => a.Decision).IsRequired();
            builder.Property(a => a.Comment).HasMaxLength(WorkflowAction.MaxCommentLength);
            builder.Property(a => a.DecidedAt).IsRequired();

            builder.HasOne<WorkflowStep>().WithMany().HasForeignKey(a => a.WorkflowStepId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(a => a.WorkflowInstanceId);
        }
    }


}
