using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Notifications;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications", "notifications");

            builder.HasKey(n => n.Id);
            builder.Property(n => n.Id).ValueGeneratedNever();

            builder.Property(n => n.Type).IsRequired().HasMaxLength(Notification.MaxTypeLength);
            builder.Property(n => n.Title).IsRequired().HasMaxLength(Notification.MaxTitleLength);
            builder.Property(n => n.Content).IsRequired().HasMaxLength(Notification.MaxContentLength);
            builder.Property(n => n.ReferenceType).HasMaxLength(Notification.MaxReferenceTypeLength);
            builder.Property(n => n.IsMandatory).IsRequired();
            builder.Property(n => n.CreatedAt).IsRequired();

            builder.Ignore(n => n.DomainEvents);
            builder.Ignore(n => n.IsRead);
            builder.Ignore(n => n.HasInApp);

            builder.HasMany(n => n.Deliveries).WithOne().HasForeignKey(d => d.NotificationId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(n => n.Deliveries).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.UserId).IsRequired().OnDelete(DeleteBehavior.Restrict);

            // The same event never notifies the same user twice, even if two retries run at the same time.
            builder.HasIndex(n => new { n.UserId, n.SourceEventId, n.Type })
                .IsUnique()
                .HasFilter("[SourceEventId] IS NOT NULL")
                .HasDatabaseName("UX_Notifications_Source");

            builder.HasIndex(n => new { n.UserId, n.ReadAt, n.CreatedAt });
        }
    }


}
