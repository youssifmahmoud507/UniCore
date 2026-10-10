using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity.Configurations
{
    public sealed class PasswordResetOtpConfiguration : IEntityTypeConfiguration<PasswordResetOtp>
    {
        public void Configure(EntityTypeBuilder<PasswordResetOtp> builder)
        {
            builder.ToTable("PasswordResetOtps", "identity");

            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).ValueGeneratedNever();

            builder.Property(o => o.OtpHash).IsRequired().HasMaxLength(64);
            builder.Property(o => o.RequestedByIp).HasMaxLength(45);

            // Concurrency token: two parallel verifications with the right OTP cannot both succeed.
            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.HasIndex(o => new { o.UserId, o.CreatedAt });
            builder.HasIndex(o => o.ExpiresAt); // for the cleanup job

            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
