using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.Infrastructure.Persistence
{
    public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Identity's default mapping first, then ours so it can override it.
            base.OnModelCreating(modelBuilder);

            // One IEntityTypeConfiguration per entity, picked up from this assembly.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            // Identity store tables we do not own: move them into the "identity" schema.
            modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles", "identity");
            modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims", "identity");
            modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins", "identity");
            modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims", "identity");
            modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens", "identity");
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // conventions.md: enums stored as strings (max 50), money is decimal(18,2).
            configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(50);

            configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        }
    }
}
