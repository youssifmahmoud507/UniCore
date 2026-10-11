using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity.Configurations
{
    public sealed class ScopeAssignmentConfiguration : IEntityTypeConfiguration<ScopeAssignment>
    {
        public void Configure(EntityTypeBuilder<ScopeAssignment> builder)
        {
            builder.ToTable("ScopeAssignments", "identity");

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.CreatedAt).IsRequired();

            // HasFilter(null) removes EF's default "IS NOT NULL" filter. Without it, two identical
            // Global assignments (ScopeId is null) would not be rejected by the database.
            builder.HasIndex(s => new { s.RoleId, s.UserId, s.ScopeType, s.ScopeId }).IsUnique().HasFilter(null);
            builder.HasIndex(s => s.UserId);
            builder.HasOne<ApplicationRole>().WithMany().HasForeignKey(s => s.RoleId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        }
    }


}
