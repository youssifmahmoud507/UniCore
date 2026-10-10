using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Infrastructure.Persistence
{
    public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // conventions.md: enums stored as strings (max 50), money is decimal(18,2).
            configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(50);
            configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        }
    }
}
