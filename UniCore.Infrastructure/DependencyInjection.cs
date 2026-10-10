using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Domain.Common.Results;
using UniCore.Infrastructure.Common;
using UniCore.Infrastructure.Modules.Identity;
using UniCore.Infrastructure.Persistence;
using UniCore.Infrastructure.Persistence.Repositories;

namespace UniCore.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

            services.AddSingleton<IClock, SystemClock>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();
            services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));

            services.AddDataProtection();


            // Identity core only: JWT authentication is added in the next batch.
            services.AddIdentityCore<ApplicationUser>(IdentityOptionsSetup.Configure)
                .AddRoles<ApplicationRole>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();

            services.AddScoped<IdentitySeeder>();

            services.AddHealthChecks()
                .AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready" });

            return services;
        }

        public static async Task<Result> SeedIdentityAsync(this IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var seeder = scope.ServiceProvider.GetService<IdentitySeeder>();
            if (seeder is null)
            {
                return Result.Fail(Error.Failure("IDENTITY_SEEDER_MISSING", "The identity seeder is not registered."));
            }

            return await seeder.SeedAsync();
        }
    }
}
