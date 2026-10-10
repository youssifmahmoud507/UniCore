using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Identity;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.Infrastructure.Persistence.Configurations
{
    public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("RefreshTokens", "identity");

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();

            builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);
            builder.Property(t => t.CreatedByIp).HasMaxLength(45);
            builder.Property(t => t.RevokedByIp).HasMaxLength(45);

            builder.HasIndex(t => t.TokenHash).IsUnique();
            builder.HasIndex(t => t.UserId);
            builder.HasIndex(t => t.ExpiresAt); // for the cleanup job

            // Users are deactivated, never deleted, so no cascade.
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
