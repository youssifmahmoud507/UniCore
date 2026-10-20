using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Notifications;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
    {
        public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
        {
            builder.ToTable("NotificationDeliveries", "notifications");

            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedNever();

            builder.Property(d => d.Channel).IsRequired();
            builder.Property(d => d.Status).IsRequired();
            builder.Property(d => d.AttemptCount).IsRequired();
            builder.Property(d => d.CreatedAt).IsRequired();
            builder.Property(d => d.ErrorMessage).HasMaxLength(NotificationDelivery.MaxErrorLength);

            // One delivery per channel per notification.
            builder.HasIndex(d => new { d.NotificationId, d.Channel }).IsUnique();
        }
    }


}
