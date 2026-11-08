using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Idempotency;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
    {
        public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
        {
            builder.ToTable("IdempotencyRecords", "platform");

            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Key).IsRequired().HasMaxLength(IdempotencyRecord.MaxKeyLength);
            builder.Property(r => r.RequestHash).IsRequired().HasMaxLength(IdempotencyRecord.HashLength);
            builder.Property(r => r.Status).IsRequired();
            builder.Property(r => r.ResponseContentType).HasMaxLength(IdempotencyRecord.MaxContentTypeLength);
            builder.Property(r => r.ResponseLocation).HasMaxLength(IdempotencyRecord.MaxLocationLength);

            // One row per user and key. This is what makes two parallel identical requests safe.
            builder.HasIndex(r => new { r.UserId, r.Key }).IsUnique().HasDatabaseName("UX_IdempotencyRecords_UserId_Key");

            // The cleanup job deletes by expiry.
            builder.HasIndex(r => r.ExpiresAt);
        }
    }

}
