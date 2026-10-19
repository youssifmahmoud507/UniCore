using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
    {
        public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
        {
            builder.ToTable("WorkflowDefinitions", "workflow");

            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();

            builder.Property(d => d.Code).IsRequired().HasMaxLength(WorkflowDefinition.MaxCodeLength);
            builder.Property(d => d.Name).IsRequired().HasMaxLength(WorkflowDefinition.MaxNameLength);
            builder.Property(d => d.Version).IsRequired();
            builder.Property(d => d.IsActive).IsRequired();
            builder.Property(d => d.CreatedAt).IsRequired();

            builder.Ignore(d => d.DomainEvents);

            builder.HasMany(d => d.Steps).WithOne().HasForeignKey(s => s.WorkflowDefinitionId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(d => d.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(d => new { d.Code, d.Version }).IsUnique();

            // Only one version of a code can be active.
            builder.HasIndex(d => d.Code).IsUnique().HasFilter("[IsActive] = 1").HasDatabaseName("UX_WorkflowDefinitions_ActiveCode");
        }
    }


}
