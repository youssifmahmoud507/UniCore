using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
    {
        public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
        {
            builder.ToTable("WorkflowInstances", "workflow");

            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).ValueGeneratedNever();

            builder.Property(i => i.DefinitionCode).IsRequired().HasMaxLength(WorkflowDefinition.MaxCodeLength);
            builder.Property(i => i.DefinitionVersion).IsRequired();
            builder.Property(i => i.ReferenceType).IsRequired().HasMaxLength(WorkflowInstance.MaxReferenceTypeLength);
            builder.Property(i => i.Status).IsRequired();
            builder.Property(i => i.CurrentStepName).HasMaxLength(WorkflowStep.MaxNameLength);
            builder.Property(i => i.CurrentRole).HasMaxLength(50);
            builder.Property(i => i.StartedAt).IsRequired();
            builder.Property(i => i.UpdatedAt).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Ignore(i => i.DomainEvents);
            builder.Ignore(i => i.IsFinished);

            builder.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(i => i.WorkflowDefinitionId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(i => i.Actions).WithOne().HasForeignKey(a => a.WorkflowInstanceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(i => i.Actions).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(i => i.Approvals).WithOne().HasForeignKey(a => a.WorkflowInstanceId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(i => i.Approvals).UsePropertyAccessMode(PropertyAccessMode.Field);

            // A request has at most one live workflow: this makes two parallel starts safe.
            builder.HasIndex(i => new { i.ReferenceType, i.ReferenceId })
                .IsUnique()
                .HasFilter("[Status] IN ('InProgress', 'Returned')")
                .HasDatabaseName("UX_WorkflowInstances_ActiveReference");

            builder.HasIndex(i => new { i.Status, i.CurrentRole });
            builder.HasIndex(i => i.RequesterUserId);
            builder.HasIndex(i => i.WorkflowDefinitionId);
        }
    }


}
