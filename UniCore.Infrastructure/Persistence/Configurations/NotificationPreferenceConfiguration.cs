using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Notifications;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
    {
        public void Configure(EntityTypeBuilder<NotificationPreference> builder)
        {
            builder.ToTable("NotificationPreferences", "notifications");

            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            builder.Property(p => p.NotificationType).IsRequired().HasMaxLength(Notification.MaxTypeLength);
            builder.Property(p => p.InAppEnabled).IsRequired();
            builder.Property(p => p.EmailEnabled).IsRequired();
            builder.Property(p => p.UpdatedAt).IsRequired();

            builder.Ignore(p => p.DomainEvents);

            builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.UserId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(p => new { p.UserId, p.NotificationType }).IsUnique();
        }
    }


}
