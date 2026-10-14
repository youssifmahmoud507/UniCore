using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Outbox;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.ToTable("OutboxMessages", "outbox");

            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedNever();

            builder.Property(m => m.Type).IsRequired().HasMaxLength(OutboxMessage.MaxTypeLength);
            builder.Property(m => m.Payload).IsRequired();
            builder.Property(m => m.LastErrorCode).HasMaxLength(OutboxMessage.MaxErrorCodeLength);

            // No RowVersion: a processor claims a message with one atomic UPDATE instead.
            builder.HasIndex(m => new { m.Status, m.NextAttemptAt });
            builder.HasIndex(m => new { m.Status, m.LockedUntil });
            builder.HasIndex(m => m.CreatedAt);
        }
    }


}
