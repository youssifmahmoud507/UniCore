using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Audit;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("AuditLogs", "audit");

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();

            builder.Property(a => a.Action).IsRequired().HasMaxLength(AuditLog.MaxActionLength);
            builder.Property(a => a.EntityName).HasMaxLength(AuditLog.MaxEntityNameLength);
            builder.Property(a => a.EntityId).HasMaxLength(AuditLog.MaxEntityIdLength);
            builder.Property(a => a.IpAddress).HasMaxLength(AuditLog.MaxIpLength);
            builder.Property(a => a.UserAgent).HasMaxLength(AuditLog.MaxUserAgentLength);
            builder.Property(a => a.OldValues).HasMaxLength(AuditLog.MaxValuesLength);
            builder.Property(a => a.NewValues).HasMaxLength(AuditLog.MaxValuesLength);
            builder.Property(a => a.CorrelationId).HasMaxLength(AuditLog.MaxCorrelationIdLength);

            // No foreign keys: the audit trail must outlive and never block the rows it describes.
            // No RowVersion: records are append-only.
            builder.HasIndex(a => a.CreatedAt);
            builder.HasIndex(a => new { a.ActorUserId, a.CreatedAt });
            builder.HasIndex(a => new { a.EntityName, a.EntityId });
            builder.HasIndex(a => new { a.Action, a.CreatedAt });
        }
    }

}
