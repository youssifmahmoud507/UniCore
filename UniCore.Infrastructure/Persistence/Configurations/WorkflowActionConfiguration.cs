using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class WorkflowActionConfiguration : IEntityTypeConfiguration<WorkflowAction>
    {
        public void Configure(EntityTypeBuilder<WorkflowAction> builder)
        {
            builder.ToTable("WorkflowActions", "workflow");

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();

            builder.Property(a => a.ActionType).IsRequired();
            builder.Property(a => a.Comment).HasMaxLength(WorkflowAction.MaxCommentLength);
            builder.Property(a => a.PerformedAt).IsRequired();

            builder.HasOne<WorkflowStep>().WithMany().HasForeignKey(a => a.WorkflowStepId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(a => new { a.WorkflowInstanceId, a.PerformedAt });
        }
    }


}
