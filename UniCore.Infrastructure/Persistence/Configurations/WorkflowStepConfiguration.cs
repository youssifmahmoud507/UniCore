using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
    {
        public void Configure(EntityTypeBuilder<WorkflowStep> builder)
        {
            builder.ToTable("WorkflowSteps", "workflow");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Property(s => s.Name).IsRequired().HasMaxLength(WorkflowStep.MaxNameLength);
            builder.Property(s => s.Order).IsRequired();
            builder.Property(s => s.RequiredRole).IsRequired().HasMaxLength(50);

            builder.HasIndex(s => new { s.WorkflowDefinitionId, s.Order }).IsUnique();
        }
    }


}
